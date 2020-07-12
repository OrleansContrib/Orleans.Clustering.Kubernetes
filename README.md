<p align="center">
  <img src="https://github.com/dotnet/orleans/blob/gh-pages/assets/logo.png" alt="Orleans.Clustering.Kubernetes" width="300px"> 
  <h1>Orleans Clustering Provider for Kubernetes</h1>
</p>

[![CI](https://github.com/OrleansContrib/Orleans.Clustering.Kubernetes/workflows/CI/badge.svg)](https://github.com/OrleansContrib/Orleans.Clustering.Kubernetes/actions)
[![NuGet](https://img.shields.io/nuget/v/Orleans.Clustering.Kubernetes.svg?style=flat)](http://www.nuget.org/packages/Orleans.Clustering.Kubernetes)

[Orleans](https://github.com/dotnet/orleans) is a framework that provides a straight-forward approach to building distributed high-scale computing applications, without the need to learn and apply complex concurrency or other scaling patterns. 

[Kubernetes](https://kubernetes.io/) (a.k.a. Kube or just K8s) is an open-source system for automating deployment, scaling, and management of containerized applications. In other words, it is one of the most popular container orchestrators out there.

**Orleans.Clustering.Kubernetes** is a package that use Kubernetes as a backend for Cluster Membership, making it easy to run Orleans clusters on top of Kubernetes.

# TL;DR

If you want to quickly test it, clone this repo and go to the [Samples Directory](https://github.com/OrleansContrib/Orleans.Clustering.Kubernetes/tree/master/samples) for instructions on how to run a sample cluster.

# Overview

Kubernetes has multiple ways to extend its API and one of those ways allow you to easily add custom data structures to it so it can be consumed later on by applications. Those objects are called _Custom Resources_ (CRD). The objects created based on CRDs are backed by the internal [etcd](https://coreos.com/etcd/) instance part of every Kubernetes deployment.

Two CRDs are created by this provider in order to store the Cluster Membership objects to comply with [Orleans Extended Cluster Membership Protocol](https://dotnet.github.io/orleans/Documentation/implementation/cluster_management.html). `ClusterVersion` and `Silo`. Examples on how to install each CRD can be found under the [samples folder](https://github.com/OrleansContrib/Orleans.Clustering.Kubernetes/tree/master/samples).
- [ClusterVersionCRD.yaml](https://github.com/OrleansContrib/Orleans.Clustering.Kubernetes/blob/master/src/Orleans.Clustering.Kubernetes/Definitions/ClusterVersionCRD.yaml)
- [SiloEntryCRD.yaml](https://github.com/OrleansContrib/Orleans.Clustering.Kubernetes/blob/master/src/Orleans.Clustering.Kubernetes/Definitions/SiloEntryCRD.yaml)

Those objects can be created at startup of the first silo in the cluster or, manually created by regular `.yaml` files. The package includes the two files with the required specs for each one. They **must** be deployed into the cluster before any Orleans application is deployed with this provider.

This provider uses only Kubernetes API Server to create/read those objects. By default, it uses the `In Cluster` API endpoint which is available on each `pod`.

From the security perspective, the provider uses whatever `serviceaccount` configured for the Kubernetes Deployment object by reading the API credentials from the `pod` itself. 

# Installation

Installation is performed via [NuGet](https://www.nuget.org/packages?q=Orleans.Clustering.Kubernetes)

From Package Manager:

> PS> Install-Package Orleans.Clustering.Kubernetes

.Net CLI:

> \# dotnet add package Orleans.Clustering.Kubernetes

Paket: 

> \# paket add Orleans.Clustering.Kubernetes

# Configuration

A functional Kubernetes cluster is required for this provider to work. If you don't have one yet, there are multiple (and mostly complicated) ways to deploy Kubernetes for production use and it is out of scope of this provider as there are many articles around the web on how to do it. However, if you are playing with Docker and Kubernetes for the first time or you want to build a development box, [Scott Hanselman](https://github.com/shanselman) has [a nice article showing how to easily setup Docker for Windows with Kubernetes on your machine](https://www.hanselman.com/blog/HowToSetUpKubernetesOnWindows10WithDockerForWindowsAndRunASPNETCore.aspx). Although it shows Windows 10, it can be easily adopted to Mac OSX as well.

## Custom Resource Definitions

You need to apply both `.yaml` files from the package before starting the silo. It must be done once per Kubernetes cluster.

> Note: You can also deploy the CRDs from the files on the `Definitions` directory on this repo.

## Silo
Tell Orleans runtime that we are going to use Kubernetes as our Cluster Membership Provider:

```cs
var silo = new SiloBuilder()
        ...
        .UseKubeMembership()
        ...
        .Build();
``` 

## Client

Now that our silo is up and running, the Orleans client needs to connect to the Kubernetes to look for Orleans Gateways.

```cs
var client = new ClientBuilder()
        ...
        .UseKubeGatewayListProvider() // Optionally use the configure delegate to specify the namespace where you cluster is running.
        ...
        .Build();
```

The provider will discover the cluster based on the kubernetes namespace the silo pod is running. In the case of the client, if a configure delegate with the `Namespace` property set to a non-null value is specified, it will ignore the current running pod namespace and will try to use that namespace instead.

Great! Now enjoy your Orleans application running within a Kubernetes cluster without needing an external membership provider! 

# Security considerations

This provider behaves like any regular application being hosted on Kubernetes. That means it doesn't care about the underlying kubernetes security model. In this particular provider however, it _expects_ the pod to have access to the API server. Usually this access is granted to the service account being used by the POD (for more on that check Kubernetes docs for service accounts) by enabling RBAC or whatever other authorization plugin your cluster is using.

Regardless of the authorization plugin being used, ensure the following:

1. The service account on the **Silo** pod has access to the Kubernetes API server to **read** and **write** objects (essentially `GET`, `LIST`, `PUT`, `DELETE`, `POST` permissions);
2. The service account on the **Client** pod must be able to access the Kubernetes API server to **read** objects (`GET`and `LIST` permissions).


# Contributions

PRs and feedback are **very** welcome! This repo follows the same contributions guideline as Orleans does and github issues will have `help-wanted` topics as they are coming. 
