package com.connecto.client.models;

import com.google.gson.annotations.SerializedName;

public class LoginReq {
    public String username;
    public String password;

    public LoginReq(String username, String password) {
        this.username = username;
        this.password = password;
    }
}
