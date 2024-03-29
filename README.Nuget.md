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
