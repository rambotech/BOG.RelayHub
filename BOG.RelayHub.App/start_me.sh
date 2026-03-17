#!/bin/sh

./BOG.RelayHub.App \
--MaxCountQueuedFilenames 20 \
--Listeners http://*:5050 \
--SecurityDelaySecondsFactor 15 \
--SecurityMaxInvalidTokenAttempts 3
