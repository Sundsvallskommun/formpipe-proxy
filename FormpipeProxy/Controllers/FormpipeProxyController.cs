using System;
using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Web.Http;

using Newtonsoft.Json;

using NSwag.Annotations;

using log4net;

using FormpipeProxy.Models;
using FormpipeProxy.Integration;

using FormPipe.LTA.Contracts.ServiceContracts;

namespace FormpipeProxy.Controllers
{
    public class FormpipeProxyController : ApiController
    {
        private static readonly ILog LOG = LogManager.GetLogger("FormpipeProxy");
        private static readonly ILog LOB_LOG = LogManager.GetLogger("LOB");

        private readonly IFormpipeClient client = new FormpipeClient();

        private static Func<ErrorInfo, ErrorDetails> toErrorDetails = errorInfo => new ErrorDetails()
        {
            ErrorId = errorInfo.ErrorId,
            ErrorCode = errorInfo.ErrorCode,
            ErrorMessage = errorInfo.ErrorMessage
        };

        private static Func<byte[], string> toHexString = b => BitConverter.ToString(b).Replace("-", "");
        private static Func<String, byte[]> base64ToByteArray = s => Convert.FromBase64String(s);
        private static Func<String, String> base64ToString = s => Encoding.UTF8.GetString(Convert.FromBase64String(s));

        // TODO: search endpoints ???

        [HttpPost]
        [Route("api/import")]
        [OpenApiOperation("import")]
        [SwaggerResponse(HttpStatusCode.OK, typeof(ImportResponse), Description = "Successful operation")]
        [SwaggerResponse(HttpStatusCode.InternalServerError, typeof(ImportResponse), Description = "Import failure")]
        public HttpResponseMessage Import([FromBody][Required] ImportRequest request)
        {
            LOG.Debug("----- Starting import ----------------------------");
            LOG.DebugFormat("Request JSON: {0}", JsonConvert.SerializeObject(request));
            LOG.Debug("Request:");
            LOG.DebugFormat("  UUID: {0}", request.Uuid);
            LOG.DebugFormat("  Submission agreement id: {0}", request.SubmissionAgreementId);
            LOG.DebugFormat("  Personal data flag: {0}", request.PersonalDataFlag);
            LOG.DebugFormat("  Confidentiality level: {0}", request.ConfidentialityLevel);
            LOG.DebugFormat("  Confidentiality deg. date: {0}", request.ConfidentialityDegradationDate.ToString("yyyy-MM-dd HH:mm:ss"));
            LOG.DebugFormat("  Metadata XML: {0} bytes", base64ToByteArray(request.MetadataXml).Length);
            LOB_LOG.DebugFormat("Metadata XML: {0}", base64ToString(request.MetadataXml));
            LOG.Debug("  Preservation object:");
            if (request.PreservationObject != null)
            {
                LOG.DebugFormat("    FileName: {0}", request.PreservationObject.FileName);
                LOG.DebugFormat("    FileExtension: {0}", request.PreservationObject.FileExtension);
                LOG.DebugFormat("    Data: {0} bytes", base64ToByteArray(request.PreservationObject.Data).Length);
                LOB_LOG.DebugFormat("Data: {0}", request.PreservationObject.Data);
            }

            var fileUuid = Guid.NewGuid().ToString();

            try
            {
                // Import preservation object
                LOG.Debug("----- Importing preservation object --------------");

                var importPreservationObjectResponse = ImportPreservationObject(request, fileUuid);
                if (importPreservationObjectResponse.ErrorInfo != null && importPreservationObjectResponse.ErrorInfo.ErrorCode != 0)
                {
                    var errorDetails = toErrorDetails(importPreservationObjectResponse.ErrorInfo);

                    LOG.ErrorFormat("Import preservation object failed: {0} (errorId: {1}, errorCode: {2})", errorDetails.ErrorMessage, errorDetails.ErrorId, errorDetails.ErrorCode);
                    LOG.Error("----- Import failed ------------------------------");

                    return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, errorDetails.ErrorMessage);
                }

                LOG.Debug("Preservation object imported OK");

                // Import metadata
                LOG.Debug("----- Importing metadata -------------------------");

                var importMetadataResponse = ImportMetadata(request);
                if (importMetadataResponse.ErrorInfo != null && importMetadataResponse.ErrorInfo.ErrorCode != 0)
                {
                    var errorDetails = toErrorDetails(importMetadataResponse.ErrorInfo);

                    LOG.ErrorFormat("Import metadata failed: {0} (errorId: {1}, errorCode: {2})", errorDetails.ErrorMessage, errorDetails.ErrorId, errorDetails.ErrorCode);
                    LOG.Error("----- Import failed ------------------------------");

                    return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, errorDetails.ErrorMessage);
                }

                LOG.Debug("Metadata imported OK");

                // Apply import
                LOG.Debug("----- Applying import ----------------------------");

                var applyImportResponse = ApplyImport(request, fileUuid);
                if (applyImportResponse.ErrorInfo != null && applyImportResponse.ErrorInfo.ErrorCode != 0)
                {
                    var errorDetails = toErrorDetails(applyImportResponse.ErrorInfo);

                    LOG.ErrorFormat("Apply import failed: {0} (errorId: {1}, errorCode: {2})", errorDetails.ErrorMessage, errorDetails.ErrorId, errorDetails.ErrorCode);
                    LOG.Error("----- Import failed ------------------------------");

                    return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, errorDetails.ErrorMessage);
                }

                LOG.Debug("Import applied OK");

                LOG.Debug("----- Import done --------------------------------");
                LOG.DebugFormat("ImportedFileSetId = {0}", applyImportResponse.ImportedFileSetId);

                return Request.CreateResponse(HttpStatusCode.OK, new ImportResponse()
                {
                    ImportedFileSetId = applyImportResponse.ImportedFileSetId
                });

            }
            catch (Exception ex)
            {
                LOG.Error("----- Import failed because of an exception ------");
                LOG.ErrorFormat("Message: {0}", ex.Message);
                LOG.ErrorFormat("Stack trace: {0}", ex.StackTrace);

                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, ex.Message);
            }
        }

        private ImportPreservationObjectResponse ImportPreservationObject(ImportRequest importRequest, string fileUuid)
        {
            var preservationObjectBytes = base64ToByteArray(importRequest.PreservationObject.Data);

            LOG.DebugFormat("Preservation object has {0} bytes", preservationObjectBytes.Length);

            var request = new ImportPreservationObjectRequest
            {
                SubmissionAgreementId = importRequest.SubmissionAgreementId,
                FileSetId = fileUuid,
                FileExtension = importRequest.PreservationObject.FileExtension,
                TotalFileSize = (long)preservationObjectBytes.Length,
                ChunkSize = preservationObjectBytes.Length,
                Chunk = preservationObjectBytes
            };

            return client.ImportPreservationObject(request);
        }

        private ImportMetadataFileResponse ImportMetadata(ImportRequest importRequest)
        {
            var metadataXmlBytes = base64ToByteArray(importRequest.MetadataXml);

            LOG.DebugFormat("Metadata XML has {0} bytes", metadataXmlBytes.Length);

            var request = new ImportMetadataFileRequest()
            {
                SubmissionAgreementId = importRequest.SubmissionAgreementId,
                FileSetId = importRequest.Uuid,
                Encoding = "UTF-8",
                TotalFileSize = (long)metadataXmlBytes.Length,
                Chunk = metadataXmlBytes,
                ChunkSize = metadataXmlBytes.Length
            };

            return client.ImportMetadataFile(request);
        }

        private ApplyImportResponse ApplyImport(ImportRequest importRequest, string fileUuid)
        {
            var metadataChecksum = CreateChecksum(base64ToByteArray(importRequest.MetadataXml));
            var fileChecksum = CreateChecksum(base64ToByteArray(importRequest.PreservationObject.Data));

            LOG.DebugFormat("Metadata checksum ({0}): {1}", metadataChecksum.Algorithm, toHexString(metadataChecksum.Value));
            LOG.DebugFormat("File/preservation object checksum ({0}): {1}", fileChecksum.Algorithm, toHexString(fileChecksum.Value));

            var request = new ApplyImportRequest()
            {
                SubmissionAgreementId = importRequest.SubmissionAgreementId,
                FileSetId = importRequest.Uuid,
                ImportMode = ImportMode.COMPLETE_FS,
                MetadataChecksum = metadataChecksum,
                ConfidentialityLevel = importRequest.ConfidentialityLevel,
                ConfidentialityDegradationDate = importRequest.ConfidentialityDegradationDate,
                PersonalDataFlag = importRequest.PersonalDataFlag,
                Files = new System.Collections.Generic.List<FileInfo>
                {
                    new FileInfo
                    {
                        FileId = fileUuid,
                        OriginalFileId = fileUuid + importRequest.PreservationObject.FileExtension,
                        OriginalFileName = fileUuid + importRequest.PreservationObject.FileExtension,
                        Checksum = fileChecksum
                    }
                }.ToArray()
            };

            return client.ApplyImport(request);
        }

        private static Checksum CreateChecksum(string input) => CreateChecksum(Encoding.UTF8.GetBytes(input));

        private static Checksum CreateChecksum(byte[] input)
        {
            MD5 hashString = new MD5CryptoServiceProvider();
            return new Checksum
            {
                Algorithm = "MD5",
                Value = hashString.ComputeHash(input)
            };
        }
    }
}
