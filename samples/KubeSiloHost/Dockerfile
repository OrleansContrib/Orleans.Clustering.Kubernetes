FROM mcr.microsoft.com/dotnet/runtime:7.0-alpine
WORKDIR /app
COPY ./bin/Debug/net7.0 ./
ENTRYPOINT ["dotnet", "KubeSiloHost.dll"]