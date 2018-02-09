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
