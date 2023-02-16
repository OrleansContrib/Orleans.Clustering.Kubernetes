# Orleans on Kubernetes - Samples

This directory contains 3 projects to show a practical example of Orleans running on top of Kubernetes using `Orleans.Clustering.Kubernetes` package:

1. `KubeClient` -> Regular Orleans Client.
2. `KubeSiloHost` - Orleans silo host.
3. `KubeGatewayHost` - An Orleans silo which has the gateway enabled.

> NOTE: The reason the gateway and non-gateway silos are on different projects, is just to illustrate that you don't have to expose all your pods to outside world in case you don't want it. It is not a requirement and all silos can be gateways if you want it.

## Pre-requisites

1. Docker
2. Kubernetes
3. .Net 7 SDK

## Running it

To run it first create the Kubernetes `namespace` you will use to host the sample deployments with `kubectl create namespace <namespace>`. Take a note of the `<namespace>` used so it can be used later on.

From the `samples/` directory, run the following commands to publish the .Net Core applications:

1. `dotnet publish -c Release KubeClient -o output/KubeClient`
2. `dotnet publish -c Release KubeGatewayHost -o output/KubeGatewayHost`
3. `dotnet publish -c Release KubeSiloHost -o output/KubeSiloHost`

Each project contains a regular `Dockerfile` which must be built to a Docker image just like a regular docker application.

To build the images, run the following commands from the `samples/` directory:

1. `docker build -f output/KubeClient/Dockerfile -t kubeclient output/KubeClient`
2. `docker build -f output/KubeGatewayHost/Dockerfile -t kubegateway output/KubeGatewayHost`
3. `docker build -f output/KubeSiloHost/Dockerfile -t kubesilo output/KubeSiloHost`

Now you have 3 images containing the 3 sample projects built on your local Docker image repository.

In order for the provider to work properly, the CRD files must be deployed. This must be done once per Kubernetes cluster regardless of how many Orleans clusters/deployments on it.

From the `samples/` directory, run the following command:

1. `kubectl apply -f ../src/Orleans.Clustering.Kubernetes/Definitions/ClusterVersionCRD.yaml`
2. `kubectl apply -f ../src/Orleans.Clustering.Kubernetes/Definitions/SiloEntryCRD.yaml`

Now you need to make sure the pods can create objects using those deifnitions. To do that, the pods must run under a service account which has access to Kubernetes APIs under the scope of those objects. To deploy the samples service accounts run the following:

1. `kubectl apply -f ./Definitions/Silo-ServiceAccount.yaml --namespace <namespace>`
2. `kubectl apply -f ./Definitions/Client-ServiceAccount.yaml --namespace <namespace>`

Those definitions create the a Kubernetes ClusterRole with permissions to read for the client, and read/write for the silos and also bind it to the service accounts you just created. You can modify the service account names and the way you create them on your production envinronment but be aware of the permissions required in order for this to work.

Now you have all the assets deployed to your Kubernetes cluster and all you need is to create the Deployment objects with the following commands:

1. `kubectl apply -f ./Definitions/Silo.yaml --namespace <namespace>`
2. `kubectl apply -f ./Definitions/Gateway.yaml --namespace <namespace>`
3. `kubectl apply -f ./Definitions/Client.yaml --namespace <namespace>`

You are all set! You can use commands like `kubectl get pods --namespace <namespace>` and you will see the client, silo and gateway pods listed on it.

To inspect the cluster objects deployed to kubertes with `kubectl get silos --namespace <namespace> -o yaml` or `kubectl get clusterversions --namespace <namespace> -o yaml` and that will return Orleans cluster membership objects in YAML (you can change to `-o json` if you like to).

Enjoy!
