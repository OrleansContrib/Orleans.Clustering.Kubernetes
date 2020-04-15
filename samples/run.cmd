kubectl run kubesilo --image=kubesilo:latest --image-pull-policy=Never
kubectl run kubehost --image=kubehost:latest --image-pull-policy=Never
kubectl run kubeclient --image=kubeclient:latest --image-pull-policy=Never

