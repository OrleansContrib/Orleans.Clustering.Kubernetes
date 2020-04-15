# Orleans on Kubernetes - Samples

This directory contains 3 projects to show a practical example of Orleans running on top of Kubernetes using `Orleans.Clustering.Kubernetes` package:

1. `KubeClient` -> Regular Orleans Client.
2. `KubeSiloHost` - Orleans silo host.
3. `KubeGatewayHost` - An Orleans silo which has the gateway enabled.

> NOTE: The reason the gateway and non-gateway silos are on different projects, is just to illustrate that you don't have to expose all your pods to outside world in case you don't want it. It is not a requirement and all silos can be gateways if you want it.

## Pre-requisites

1. Docker
2. Kubernetes
3. .Net Core 2.0 SDK

## Running it

To run it first create the Kubernetes `namespace` you will use to host the sample deployments with `kubectl create namespace <namespace>`. Take a note of the `<namespace>` used so it can be used later on.  To make it easier you can make this your default namespace using `kubectl config set-context --current --namespace <namespace>` so that subsequent `kubectl` commands do not need to specify the `--namespace` option.

Each project contains a regular `Dockerfile` which must be built to a Docker image just like a regular docker application.

1. Publish each project using `dotnet publish -c Release -o PublishOutput`
2. From the `PublishOutput` directory of each project, build the image with `docker build -t <imagename>:<imagetag> .` and replace `<imagename>` and `<imagetag>` with the respective project name (i.e `kubesilo`, `kubehost`, `kubeclient` for the name and `latest` for the tag) or whatever name you want.
3. For each project run `kubectl run <servicename> --image=<imagename>:<imagetag> --namespace=<namespace> --image-pull-policy=Never`. First the silo, later the gateway then the client. Be sure to replace the `<xxx>` tags with the values used on previous steps.

See `prep.cmd`, `run.cmd` and `cleanup.cmd` for an example of the complete process (assumes you set a default `--namespace`).

> NOTE: The reason we use `--image-pull-policy=Never` for this sample is just so Kubernetes doesn't try to pull the image from Docker Hub.

## Test output

Run this command to find your running pods: `kubectl get pods --namespace=<namespace>` and the use the name of the `kubeclient` pod in a `kubectl logs` command, for example: `kubectl logs kubeclient-5965d9c5f-xqbgg  --namespace=<namespace>` and you will see a the log output like this showing that the client did successfully activate a Grain on the Silo and was able
to call that grain 1000 times.
```
...
You said: 'Good morning, my friend!', I say: Hello!: 996
You said: 'Good morning, my friend!', I say: Hello!: 997
You said: 'Good morning, my friend!', I say: Hello!: 998
You said: 'Good morning, my friend!', I say: Hello!: 999
KubeClient test is completed, avg time = 0.7285 milliseconds.
```
