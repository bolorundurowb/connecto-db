package com.connecto.client.models;

import com.google.gson.annotations.SerializedName;
import java.time.OffsetDateTime;
import java.util.UUID;

public class UserRes {
    public UUID id;
    public String firstName;
    public String lastName;
    public String username;
    @SerializedName("createdAt")
    public OffsetDateTime createdAt;
}
