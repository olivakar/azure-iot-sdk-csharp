# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

print("---------------------------")

data_amqp = subset(data, subset = Protocol == "Amqp")
data_mqtt = subset(data, subset = Protocol == "Mqtt")
data_http = subset(data, subset = Protocol == "Http1")

print(summary(data_amqp))
print(summary(data_mqtt))
print(summary(data_http))
