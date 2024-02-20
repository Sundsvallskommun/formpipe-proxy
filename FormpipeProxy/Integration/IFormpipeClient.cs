using FormPipe.LTA.Contracts.ServiceContracts;

namespace FormpipeProxy.Integration
{
    public interface IFormpipeClient
    {
        SystemVersionInfoResponse GetSystemVersion();

        ImportMetadataResponse ImportMetadata(ImportMetadataRequest request);

        ImportMetadataFileResponse ImportMetadataFile(ImportMetadataFileRequest request);

        ImportPreservationObjectResponse ImportPreservationObject(ImportPreservationObjectRequest request);

        ApplyImportResponse ApplyImport(ApplyImportRequest request);

        GetAllSubmissionAgreementsResponse GetAllSubmissionAgreements();

        GetAllChecksumAlgorithmsResponse GetAllChecksumAlgorithms(GetAllChecksumAlgorithmsRequest request);
    }
}
