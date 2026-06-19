use std::collections::HashMap;

use serde_json::{json, Value};

use crate::types::*;
use crate::hub::HubConnection;

pub type FlexMap = HashMap<String, Value>;

pub struct ConnectoClient {
    base_url: String,
    http: reqwest::Client,
    token: Option<String>,
    data_hub: Option<HubConnection>,
    collection_hub: Option<HubConnection>,
}

impl ConnectoClient {
    pub fn new(url: &str) -> Self {
        Self {
            base_url: url.trim_end_matches('/').to_string(),
            http: reqwest::Client::new(),
            token: None,
            data_hub: None,
            collection_hub: None,
        }
    }

    pub fn is_connected(&self) -> bool {
        self.data_hub.is_some() && self.collection_hub.is_some()
    }

    // ── Auth ────────────────────────────────────────────────────

    pub async fn login(&mut self, username: &str, password: &str) -> Result<AuthRes, String> {
        let req = LoginReq {
            username: username.to_string(),
            password: password.to_string(),
        };
        let res = self
            .http
            .post(format!("{}/api/auth/login", self.base_url))
            .json(&req)
            .send()
            .await
            .map_err(|e| e.to_string())?;

        if !res.status().is_success() {
            let body: GenericRes = res.json().await.map_err(|e| e.to_string())?;
            return Err(body.message);
        }

        let auth: AuthRes = res.json().await.map_err(|e| e.to_string())?;
        self.token = Some(auth.token.clone());
        Ok(auth)
    }

    pub async fn register(
        &mut self,
        username: &str,
        password: &str,
        first_name: Option<&str>,
        last_name: Option<&str>,
    ) -> Result<AuthRes, String> {
        let req = RegisterReq {
            username: username.to_string(),
            password: password.to_string(),
            first_name: first_name.map(|s| s.to_string()),
            last_name: last_name.map(|s| s.to_string()),
        };
        let res = self
            .http
            .post(format!("{}/api/auth/register", self.base_url))
            .json(&req)
            .send()
            .await
            .map_err(|e| e.to_string())?;

        if !res.status().is_success() {
            let body: GenericRes = res.json().await.map_err(|e| e.to_string())?;
            return Err(body.message);
        }

        let auth: AuthRes = res.json().await.map_err(|e| e.to_string())?;
        self.token = Some(auth.token.clone());
        Ok(auth)
    }

    // ── Connect ─────────────────────────────────────────────────

    pub async fn connect(&mut self) -> Result<(), String> {
        let token = self.token.as_ref().ok_or("Not authenticated")?;
        self.data_hub = Some(HubConnection::connect(&self.base_url, "/data-stream", token).await?);
        self.collection_hub = Some(HubConnection::connect(&self.base_url, "/collection-stream", token).await?);
        Ok(())
    }

    // ── Collections ─────────────────────────────────────────────

    pub async fn list_tables(&self) -> Result<Vec<String>, String> {
        let hub = self.collection_hub.as_ref().ok_or("Not connected")?;
        let result = hub.invoke("ListTables", vec![]).await?;
        serde_json::from_value(result).map_err(|e| e.to_string())
    }

    pub async fn create_table(&self, name: &str) -> Result<String, String> {
        let hub = self.collection_hub.as_ref().ok_or("Not connected")?;
        hub.invoke("CreateTable", vec![json!(name)]).await?;
        let val = hub.wait_for_broadcast("TableCreated").await?;
        Ok(val.as_str().unwrap_or("").to_string())
    }

    pub async fn delete_table(&self, name: &str) -> Result<String, String> {
        let hub = self.collection_hub.as_ref().ok_or("Not connected")?;
        hub.invoke("DeleteTable", vec![json!(name)]).await?;
        let val = hub.wait_for_broadcast("TableDeleted").await?;
        Ok(val.as_str().unwrap_or("").to_string())
    }

    // ── Data ────────────────────────────────────────────────────

    pub async fn subscribe(&self, table: &str) -> Result<(), String> {
        let hub = self.data_hub.as_ref().ok_or("Not connected")?;
        hub.invoke("SubscribeToTable", vec![json!(table)]).await?;
        Ok(())
    }

    pub async fn unsubscribe(&self, table: &str) -> Result<(), String> {
        let hub = self.data_hub.as_ref().ok_or("Not connected")?;
        hub.invoke("UnsubscribeFromTable", vec![json!(table)]).await?;
        Ok(())
    }

    pub async fn get_all_records(&self, table: &str) -> Result<Vec<FlexMap>, String> {
        let hub = self.data_hub.as_ref().ok_or("Not connected")?;
        let result = hub.invoke("GetAllRecords", vec![json!(table)]).await?;
        serde_json::from_value(result).map_err(|e| e.to_string())
    }

    pub async fn get_record(&self, table: &str, id: &str) -> Result<FlexMap, String> {
        let hub = self.data_hub.as_ref().ok_or("Not connected")?;
        let result = hub.invoke("GetRecord", vec![json!(table), json!(id)]).await?;
        serde_json::from_value(result).map_err(|e| e.to_string())
    }

    pub async fn upsert(&self, table: &str, data: FlexMap) -> Result<FlexMap, String> {
        let hub = self.data_hub.as_ref().ok_or("Not connected")?;
        let result = hub.invoke("UpsertDataRecord", vec![json!(table), json!(data)]).await?;
        serde_json::from_value(result).map_err(|e| e.to_string())
    }

    pub async fn delete_record(&self, table: &str, id: &str) -> Result<String, String> {
        let hub = self.data_hub.as_ref().ok_or("Not connected")?;
        let result = hub.invoke("DeleteRecord", vec![json!(table), json!(id)]).await?;
        Ok(result.as_str().unwrap_or("").to_string())
    }

    // ── Realtime Listeners ──────────────────────────────────────

    pub fn on_entity_created(&self, cb: impl Fn(String, FlexMap) + Send + 'static) {
        if let Some(hub) = &self.data_hub {
            let tx = hub.broadcast_tx.clone();
            tokio::spawn(async move {
                loop {
                    let result = HubConnection::wait_for_broadcast_from(&tx, "EntityCreated").await;
                    if let Ok(val) = result {
                        let data: FlexMap = serde_json::from_value(val).unwrap_or_default();
                        cb(String::new(), data);
                    }
                }
            });
        }
    }
}
