# Copyright(c) Microsoft. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for
# full license information.

# Select R Tools, Windows Packages (Ctrl-7) and add "ggplot2".
#install.packages('ggplot2')

library(ggplot2)
library(plyr)

raw = read.csv('../DeviceTelemetryTest/bin/Debug/netcoreapp2.1/singleDeviceTelemetryPerf.csv')
print(summary(raw))

totalMessages = 10000
messageSize = 64
data = raw

#source("showdetails.R")
source("showhistogram.R")
#source("showjitter.R")
