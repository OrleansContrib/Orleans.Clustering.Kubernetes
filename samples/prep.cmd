cd KubeSiloHost
dotnet publish -c Release -o PublishOutput
cd PublishOutput
docker build -t kubesilo:latest .

cd ..\..\KubeGatewayHost
dotnet publish -c Release -o PublishOutput
cd PublishOutput
docker build -t kubehost:latest .

cd  ..\..\KubeClient
dotnet publish -c Release -o PublishOutput
cd PublishOutput
docker build -t kubeclient:latest .

cd ..\..