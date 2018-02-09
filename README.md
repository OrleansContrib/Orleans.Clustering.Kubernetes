<p align="center">
  <img src="https://github.com/dotnet/orleans/blob/gh-pages/assets/logo.png" alt="Orleans.Clustering.Kubernetes" width="300px"> 
  <h1>Orleans Clustering Provider for Kubernetes</h1>
</p>


[![CircleCI](https://circleci.com/gh/OrleansContrib/Orleans.Clustering.Kubernetes.svg?style=svg)](https://circleci.com/gh/OrleansContrib/Orleans.Clustering.Kubernetes)
[![NuGet](https://img.shields.io/nuget/v/Orleans.Clustering.Kubernetes.svg?style=flat)](http://www.nuget.org/packages/Orleans.Clustering.Kubernetes)

[Orleans](https://github.com/dotnet/orleans) is a framework that provides a straight-forward approach to building distributed high-scale computing applications, without the need to learn and apply complex concurrency or other scaling patterns. 

[Kubernetes](https://kubernetes.io/) (a.k.a. Kube or just K8s) is an open-source system for automating deployment, scaling, and management of containerized applications. In other words, it is one of the most popular container orchestrators out there.

**Orleans.Clustering.Kubernetes** is a package that use Kubernetes as a backend for Cluster Membership, making it easy to run Orleans clusters on top of Kubernetes.

# TL;DR

If you want to quickly test it, clone this repo and go to the [Samples Directory](https://github.com/OrleansContrib/Orleans.Clustering.Kubernetes/tree/master/samples) for instructions on how to run a sample cluster.

# Overview

Kubernetes has multiple ways to extend its API and one of those ways allow you to easily add custom data structures to it so it can be consumed later on by applications. Those objects are called _Custom Resources_ (CRD). The objects created based on CRDs are backed by the internal [etcd](https://coreos.com/etcd/) instance part of every Kubernetes deployment.

Two CRDs are created by this provider in order to store the Cluster Membership objects to comply with [Orleans Extended Cluster Membership Protocol](http://dotnet.github.io/orleans/Documentation/Runtime-Implementation-Details/Cluster-Management.html). `ClusterVersion` and `Silo`. 

Those objects can be created at startup of the first silo in the cluster or, manually created by regular `.yaml` files. The package includes the two files with the required specs for each one. It may be useful for scenarios where the deployment is running under a very restrict Service Account, so you have to use them to create the CRDs upfront.

This provider uses only Kubernetes API Server to create/read those objects. By default, it uses the `In Cluster` API endpoint which is available on each `pod` but if required, can use whatever endpoint you specify at the provider options. That is useful if you want to rename the endpoint system DNS name or use a sidecar container that proxy all your requests to the real API server. 

From the security perspective, the provider uses whatever `serviceaccount` configured for the Kubernetes Deployment object by reading the API credentials from the `pod` itself. In case you configured Kubernetes to not inject the credentials into the `pod`, you can always specify the CA certificate and API token along with the API endpoint at the provider options object.   

# Installation

Installation is performed via [NuGet](https://www.nuget.org/packages?q=Orleans.Clustering.Kubernetes)

From Package Manager:

> PS> Install-Package Orleans.Clustering.Kubernetes -prerelease

.Net CLI:

> \# dotnet add package Orleans.Clustering.Kubernetes -prerelease

Paket: 

> \# paket add Orleans.Clustering.Kubernetes -prerelease

# Configuration

A functional Kubernetes cluster is required for this provider to work. If you don't have one yet, there are multiple (and mostly complicated) ways to deploy Kubernetes for production use and it is out of scope of this provider as there are many articles around the web on how to do it. However, if you are playing with Docker and Kubernetes for the first time or you want to build a development box, [Scott Hanselman](https://github.com/shanselman) has [a nice article showing how to easily setup Docker for Windows with Kubernetes on your machine](https://www.hanselman.com/blog/HowToSetUpKubernetesOnWindows10WithDockerForWindowsAndRunASPNETCore.aspx). Although it show Windows 10, it can be easily adopted to Mac OSX as well.

## Silo
Tell Orleans runtime that we are going to use Kubernetes as our Cluster Membership Provider:

```cs
var silo = new SiloHostBuilder()
        ...
        .UseKubeMembership(opt =>
        {
            opt.CanCreateResources = true;
        })
        ...
        .Build();
``` 

The `CanCreateResources` will tell the provider to try create the CRDs at the startup time. In case it is set to false, you need to apply both `.yaml` files from the package before start the silo. It must be done once per Kubernetes cluster.

## Client

Now that our silo is up and running, the Orleans client needs to connect to the Kubernetes to look for Orleans Gateways.

```cs
var client = new ClientBuilder()
        ...
        .UseKubeGatewayListProvider()
        ...
        .Build();
```

Both gateway list and the membership provider has other options that allow you to specify credentials and the API endpoint for your Kubernetes API server. The default will use everything discovered from the data injecte from Kubernetes runtime into the `pod`.

Great! Now enjoy your Orleans application running within a Kubernetes cluster without need an external membership provider! 

# Contributions

PRs and feedback are **very** welcome! This repo follows the same contributions guideline as Orleans does and github issues will have `help-wanted` topics as they are coming. 