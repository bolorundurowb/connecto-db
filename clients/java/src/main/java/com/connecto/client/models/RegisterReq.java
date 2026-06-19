package com.connecto.client.models;

import com.google.gson.annotations.SerializedName;

public class RegisterReq {
    public String username;
    public String password;
    @SerializedName("firstName")
    public String firstName;
    @SerializedName("lastName")
    public String lastName;

    public RegisterReq(String username, String password) {
        this.username = username;
        this.password = password;
    }
}
