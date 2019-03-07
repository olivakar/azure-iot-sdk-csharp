# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

print(qplot(data$Protocol, data$AckMs, geom = c("jitter"), color = data$Protocol))
