using System.Net;
using System.ServiceModel.Security;
using System.Configuration;

using FormPipe.LTA.Contracts.ServiceContracts;

namespace FormpipeProxy.Integration
{
    public class FormpipeClient : IFormpipeClient
    {
        private readonly ImportWebServiceClient importClient;

        public FormpipeClient()
        {
            importClient = new ImportWebServiceClient();
            importClient.ClientCredentials.UserName.UserName = ConfigurationManager.AppSettings["FormPipeUsername"];
            importClient.ClientCredentials.UserName.Password = ConfigurationManager.AppSettings["FormPipePassword"];

            // Disable certificate validation and override the server certificate validation callback with a dummy
            importClient.ClientCredentials.ServiceCertificate.Authentication.CertificateValidationMode = X509CertificateValidationMode.None;
            ServicePointManager.ServerCertificateValidationCallback = (sender, cert, chain, sslPolicyErrors) => true;
        }

        public SystemVersionInfoResponse GetSystemVersion() => importClient.GetSystemVersion();

        public ImportMetadataResponse ImportMetadata(ImportMetadataRequest request) => importClient.ImportMetadata(request);

        public ImportMetadataFileResponse ImportMetadataFile(ImportMetadataFileRequest request) => importClient.ImportMetadataFile(request);

        public ImportPreservationObjectResponse ImportPreservationObject(ImportPreservationObjectRequest request) => importClient.ImportPreservationObject(request);

        public ApplyImportResponse ApplyImport(ApplyImportRequest request) => importClient.ApplyImport(request);

        public GetAllSubmissionAgreementsResponse GetAllSubmissionAgreements() => importClient.GetAllSubmissionAgreements();

        public GetAllChecksumAlgorithmsResponse GetAllChecksumAlgorithms(GetAllChecksumAlgorithmsRequest request) => importClient.GetAllChecksumAlgorithms(request);
    }
}
