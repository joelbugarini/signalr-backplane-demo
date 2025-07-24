@echo off
cd SignalRBackplaneDemo.Server
dotnet run --launch-profile Replica1
start cmd /k "cd SignalRBackplaneDemo.Server && dotnet run --launch-profile Replica2" 