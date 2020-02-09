using Orleans.Clustering.Kubernetes.API;
using Xunit;

namespace Orleans.Clustering.Kubernetes.Test
{
    public class KubeClientTests
    {
        [Fact]
        public void RootCertificateConstructedFromCertificateParameterWhenProvided_ReturnsValidRootCertificate()
        {
            const string certificate =
                @"-----BEGIN CERTIFICATE-----
        MIIC+zCCAeOgAwIBAgIJAIC+9fYZ2Xu1MA0GCSqGSIb3DQEBBQUAMBQxEjAQBgNV
        BAMMCWxvY2FsaG9zdDAeFw0xOTEwMDYxMjE2MDFaFw0yOTEwMDMxMjE2MDFaMBQx
        EjAQBgNVBAMMCWxvY2FsaG9zdDCCASIwDQYJKoZIhvcNAQEBBQADggEPADCCAQoC
        ggEBALWQJx3SZiW4plqZSpjz0PFU9QR3IcK27Du6Tr7wlBErfdEdUJ/TuC72EFWF
        yR4/pCWfmiIwZnkxJGVwjaRbi0B/hza9ifbr+7uqxlVYZUN8lt5UtFpFuRvs5g69
        uEutlJx/dUmvPQOoW1DzmqUAKlum8vzhcRZaSYKtWnLyGb0ec20B6Sa1nrEQtW0h
        LofTJ6JvTk+MZmCTzIfeBsNz2iLDdSYDr7rlP/lWkJh+XjzFPBCDJILP1JUwPOrF
        AUpwuejqm4BecjLlquPN8Lqpf7Zp1X5WCU81AQyuHXlv3FdayL8VTJGhoTRpCrvU
        s5WnZgrOq3IAAk8agme+JbL6NqUCAwEAAaNQME4wHQYDVR0OBBYEFMNqhycFj+RF
        LmxsR4EMu0mpnJL3MB8GA1UdIwQYMBaAFMNqhycFj+RFLmxsR4EMu0mpnJL3MAwG
        A1UdEwQFMAMBAf8wDQYJKoZIhvcNAQEFBQADggEBADqFsU9l2PJsCiGtU4C3tsAz
        LwHUEvOHRjXFU9bN8LApyUIGo9qsTXLyLOLv0nIrDq1gELoKgAvuD1lbnWbcTcCG
        q5/f9ttB6cT513ffm4GBTenTIHSq+FFZT6A2Mwko1+3Man2vznWjl/HF5eQ6PS7U
        by/z6A9Nm1Rw8Uu541odWU8/KQCSO9c/Yo+1GsDIDj5lMfLRl21Mp/lPGbGGuGu5
        iMRH8QYi7YQZ2GLbaLxSY7edJlmFGYTWWV9C9jKE3s/WMaJ6Dz7NXrQM4YOE00V8
        bzUuNqNBUDUhQGQDOCem29+nhKT+GPWqDt2YRDFj80UJ5wt+ljddmlVhtRS/YgY=
        -----END CERTIFICATE-----";

            var client = new KubeClient(null, certificate: certificate);

            Assert.NotNull(client.RootCertificate);
        }

        [Fact]
        public void RootCertificateConstructedFromCertificateWithoutHeaders_ReturnsValidRootCertificate()
        {
            const string certificate = @"MIIC+zCCAeOgAwIBAgIJAIC+9fYZ2Xu1MA0GCSqGSIb3DQEBBQUAMBQxEjAQBgNVBAMMCWxvY2FsaG9zdDAeFw0xOTEwMDYxMjE2MDFaFw0yOTEwMDMxMjE2MDFaMBQxEjAQBgNVBAMMCWxvY2FsaG9zdDCCASIwDQYJKoZIhvcNAQEBBQADggEPADCCAQoCggEBALWQJx3SZiW4plqZSpjz0PFU9QR3IcK27Du6Tr7wlBErfdEdUJ/TuC72EFWFyR4/pCWfmiIwZnkxJGVwjaRbi0B/hza9ifbr+7uqxlVYZUN8lt5UtFpFuRvs5g69uEutlJx/dUmvPQOoW1DzmqUAKlum8vzhcRZaSYKtWnLyGb0ec20B6Sa1nrEQtW0hLofTJ6JvTk+MZmCTzIfeBsNz2iLDdSYDr7rlP/lWkJh+XjzFPBCDJILP1JUwPOrFAUpwuejqm4BecjLlquPN8Lqpf7Zp1X5WCU81AQyuHXlv3FdayL8VTJGhoTRpCrvUs5WnZgrOq3IAAk8agme+JbL6NqUCAwEAAaNQME4wHQYDVR0OBBYEFMNqhycFj+RFLmxsR4EMu0mpnJL3MB8GA1UdIwQYMBaAFMNqhycFj+RFLmxsR4EMu0mpnJL3MAwGA1UdEwQFMAMBAf8wDQYJKoZIhvcNAQEFBQADggEBADqFsU9l2PJsCiGtU4C3tsAzLwHUEvOHRjXFU9bN8LApyUIGo9qsTXLyLOLv0nIrDq1gELoKgAvuD1lbnWbcTcCGq5/f9ttB6cT513ffm4GBTenTIHSq+FFZT6A2Mwko1+3Man2vznWjl/HF5eQ6PS7Uby/z6A9Nm1Rw8Uu541odWU8/KQCSO9c/Yo+1GsDIDj5lMfLRl21Mp/lPGbGGuGu5iMRH8QYi7YQZ2GLbaLxSY7edJlmFGYTWWV9C9jKE3s/WMaJ6Dz7NXrQM4YOE00V8bzUuNqNBUDUhQGQDOCem29+nhKT+GPWqDt2YRDFj80UJ5wt+ljddmlVhtRS/YgY=";

            var client = new KubeClient(null, certificate: certificate);

            Assert.NotNull(client.RootCertificate);
        }

        [IgnoreInsidePodFact]
        public void RootCertificateMissing_ReturnsNullRootCertificate()
        {
            var client = new KubeClient(null);

            Assert.Null(client.RootCertificate);
        }
    }
}
