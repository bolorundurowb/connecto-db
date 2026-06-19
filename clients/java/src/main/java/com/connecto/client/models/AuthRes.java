package com.connecto.client.models;

import com.google.gson.annotations.SerializedName;
import java.time.OffsetDateTime;

public class AuthRes {
    public UserRes user;
    public String token;
    @SerializedName("expiresAt")
    public OffsetDateTime expiresAt;
}
