use std::collections::HashMap;
use std::sync::Arc;

use futures_util::{SinkExt, StreamExt};
use serde::{Deserialize, Serialize};
use serde_json::Value;
use tokio::sync::{mpsc, oneshot, Mutex};
use tokio_tungstenite::{connect_async, tungstenite::Message, MaybeTlsStream, WebSocketStream};
use tokio::net::TcpStream;
use url::Url;

const RECORD_SEPARATOR: char = '\x1e';

#[derive(Serialize)]
#[serde(rename_all = "camelCase")]
struct HandshakeRequest {
    protocol: String,
    version: u32,
}

#[derive(Deserialize)]
struct CompletionFrame {
    #[serde(rename = "type")]
    msg_type: u32,
    invocation_id: Option<String>,
    result: Option<Value>,
    error: Option<String>,
    target: Option<String>,
    arguments: Option<Vec<Value>>,
}

pub type WsWrite = futures_util::stream::SplitSink<WebSocketStream<MaybeTlsStream<TcpStream>>, Message>;
pub type WsRead = futures_util::stream::SplitStream<WebSocketStream<MaybeTlsStream<TcpStream>>>;

pub struct HubConnection {
    pub write_tx: mpsc::UnboundedSender<String>,
    pub broadcast_tx: mpsc::UnboundedSender<(String, Value)>,
    invocations: Arc<Mutex<HashMap<String, oneshot::Sender<Result<Value, String>>>>>,
    broadcast_rx: Arc<Mutex<mpsc::UnboundedReceiver<(String, Value)>>>,
}

impl HubConnection {
    pub async fn connect(base_url: &str, path: &str, token: &str) -> Result<Self, String> {
        let ws_url = base_url
            .replace("http://", "ws://")
            .replace("https://", "wss://");
        let url = Url::parse(&format!("{}{}?access_token={}", ws_url, path, token))
            .map_err(|e| e.to_string())?;

        let (ws, _) = connect_async(url).await.map_err(|e| e.to_string())?;
        let (mut write, read) = ws.split();

        // Send handshake
        let handshake = serde_json::to_string(&HandshakeRequest {
            protocol: "json".to_string(),
            version: 1,
        })
        .unwrap();
        write
            .send(Message::Text(format!("{}{}", handshake, RECORD_SEPARATOR).into()))
            .await
            .map_err(|e| e.to_string())?;

        let invocations = Arc::new(Mutex::new(HashMap::new()));
        let (write_tx, mut write_rx) = mpsc::unbounded_channel::<String>();
        let (broadcast_tx, broadcast_rx) = mpsc::unbounded_channel::<(String, Value)>();

        // Spawn writer task
        let write_handle = tokio::spawn(async move {
            while let Some(msg) = write_rx.recv().await {
                let _ = write.send(Message::Text(msg.into())).await;
            }
        });

        let broadcast_rx = Arc::new(Mutex::new(broadcast_rx));

        // Spawn reader task
        let inv_clone = Arc::clone(&invocations);
        let broadcast_tx_clone = broadcast_tx.clone();
        tokio::spawn(async move {
            let mut read = read;
            while let Some(Ok(Message::Text(text))) = read.next().await {
                for frame in text.split(RECORD_SEPARATOR) {
                    if frame.is_empty() || frame == "{}" {
                        // handshake ack or empty
                        continue;
                    }
                    if let Ok(msg) = serde_json::from_str::<CompletionFrame>(frame) {
                        match msg.msg_type {
                            1 => {
                                // Invocation from server -> broadcast event
                                if let (Some(target), Some(args)) = (msg.target, msg.arguments) {
                                    let val = if args.len() == 1 {
                                        args.into_iter().next().unwrap()
                                    } else {
                                        Value::Array(args)
                                    };
                                    let _ = broadcast_tx_clone.send((target, val));
                                }
                            }
                            3 => {
                                // Completion
                                if let Some(inv_id) = msg.invocation_id {
                                    let mut map = inv_clone.lock().await;
                                    if let Some(tx) = map.remove(&inv_id) {
                                        match msg.error {
                                            Some(err) => { let _ = tx.send(Err(err)); }
                                            None => { let _ = tx.send(Ok(msg.result.unwrap_or(Value::Null))); }
                                        }
                                    }
                                }
                            }
                            _ => {}
                        }
                    }
                }
            }
        });

        Ok(Self {
            write_tx,
            broadcast_tx,
            invocations,
            broadcast_rx: Arc::clone(&broadcast_rx),
        })
    }

    pub async fn invoke(&self, target: &str, args: Vec<Value>) -> Result<Value, String> {
        let inv_id = uuid::Uuid::new_v4().to_string();
        let (tx, rx) = oneshot::channel();

        {
            let mut map = self.invocations.lock().await;
            map.insert(inv_id.clone(), tx);
        }

        let msg = serde_json::json!({
            "type": 1,
            "invocationId": inv_id,
            "target": target,
            "arguments": args,
        });
        let frame = format!("{}{}", serde_json::to_string(&msg).unwrap(), RECORD_SEPARATOR);
        self.write_tx.send(frame).map_err(|e| e.to_string())?;

        rx.await.map_err(|_| "Invocation cancelled".to_string())?
    }

    pub async fn wait_for_broadcast(&self, event: &str) -> Result<Value, String> {
        let mut rx = self.broadcast_rx.lock().await;
        loop {
            let (evt, val) = rx.recv().await.ok_or("Connection closed")?;
            if evt == event {
                return Ok(val);
            }
        }
    }
}
