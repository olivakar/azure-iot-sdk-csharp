# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

data_summary = ddply(data, c("Protocol"), summarise,
    mean = mean(SendMs + AckMs, na.rm = TRUE),
    med = median(SendMs + AckMs, na.rm = TRUE),
    sd = sd(SendMs + AckMs, na.rm = TRUE),
    min = min(SendMs + AckMs, na.rm = TRUE),
    max = max(SendMs + AckMs, na.rm = TRUE),
    q99 = quantile(SendMs + AckMs, 0.99, na.rm = TRUE)
    )
    
print(ggplot(data, aes(SendMs + AckMs, fill = Protocol)) +
    geom_histogram(binwidth = 0.03, alpha = 0.7) +
    scale_x_log10() +
    scale_y_sqrt() +
    xlab("Request total time (ms)") +
    ylab("Count of requests") +
    facet_grid(Protocol ~ .) +
    geom_vline(data = data_summary, aes(xintercept = data_summary$max), color = "red") +
    geom_vline(data = data_summary, aes(xintercept = data_summary$min), color = "green") +
    geom_vline(data = data_summary, aes(xintercept = data_summary$q99), linetype = "dashed") +
    geom_text(data = data_summary, aes(data_summary$q99*1.1, 0, label = sprintf("q99=%1.2f", data_summary$q99)), size = 3, angle = 45, vjust=-10, hjust=-0.2) +
    geom_text(data = data_summary, aes(data_summary$med, 0, label = sprintf("med=%1.2f\nstddev=%1.2f", data_summary$med, data_summary$sd)), size = 3, vjust=-1, hjust=0.5) +
    geom_text(data = data_summary, aes(data_summary$max*0.7, 0, label = sprintf("max=%1.2f", data_summary$max)), size = 3, angle = 45, vjust = 0.5, hjust = -0.2) +
    ggtitle(paste("Histogram of request time")))
