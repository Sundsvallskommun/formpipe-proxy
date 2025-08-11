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

using static FormpipeProxy.Util.LogUtility;

namespace FormpipeProxy.Controllers
{
    public class FormpipeProxyController : ApiController
    {
        private static readonly ILog Log = LogManager.GetLogger("FormpipeProxy");
        private static readonly ILog LobLog = LogManager.GetLogger("LOB");

        private readonly IFormpipeClient _client = new FormpipeClient();

        private static readonly Func<ErrorInfo, ErrorDetails> ToErrorDetails = errorInfo => new ErrorDetails()
        {
            ErrorId = errorInfo.ErrorId,
            ErrorCode = errorInfo.ErrorCode,
            ErrorMessage = errorInfo.ErrorMessage
        };

        private static readonly Func<byte[], string> ToHexString = b => BitConverter.ToString(b).Replace("-", "");
        private static readonly Func<String, byte[]> Base64ToByteArray = Convert.FromBase64String;
        private static readonly Func<String, String> Base64ToString = s => Encoding.UTF8.GetString(Convert.FromBase64String(s));

        // TODO: search endpoints ???

        [HttpPost]
        [Route("api/import")]
        [OpenApiOperation("import")]
        [SwaggerResponse(HttpStatusCode.OK, typeof(ImportResponse), Description = "Successful operation")]
        [SwaggerResponse(HttpStatusCode.InternalServerError, typeof(ImportResponse), Description = "Import failure")]
        public HttpResponseMessage Import([FromBody][Required] ImportRequest request)
        {
            Log.Debug("----- Starting import ----------------------------");
            Log.DebugFormat("Request JSON: {0}", SanitizeForLogging(JsonConvert.SerializeObject(request)));
            Log.Debug("Request:");
            Log.DebugFormat("  UUID: {0}", SanitizeForLogging(request.Uuid));
            Log.DebugFormat("  Submission agreement id: {0}",  SanitizeForLogging(request.SubmissionAgreementId));
            Log.DebugFormat("  Personal data flag: {0}", request.PersonalDataFlag);
            Log.DebugFormat("  Confidentiality level: {0}", request.ConfidentialityLevel);
            Log.DebugFormat("  Confidentiality deg. date: {0}", SanitizeForLogging(request.ConfidentialityDegradationDate.ToString("yyyy-MM-dd HH:mm:ss")));
            Log.DebugFormat("  Metadata XML: {0} bytes", Base64ToByteArray(request.MetadataXml).Length);
            LobLog.DebugFormat("Metadata XML: {0}", SanitizeForLogging(Base64ToString(request.MetadataXml)));
            Log.Debug("  Preservation object:");
            if (request.PreservationObject != null)
            {
                Log.DebugFormat("    FileName: {0}", SanitizeForLogging(request.PreservationObject.FileName));
                Log.DebugFormat("    FileExtension: {0}", SanitizeForLogging(request.PreservationObject.FileExtension));
                Log.DebugFormat("    Data: {0} bytes", Base64ToByteArray(SanitizeForLogging(request.PreservationObject.Data)).Length);
                LobLog.DebugFormat("Data: {0}", SanitizeForLogging(request.PreservationObject.Data));
            }

            var fileUuid = Guid.NewGuid().ToString();

            try
            {
                // Import preservation object
                Log.Debug("----- Importing preservation object --------------");

                var importPreservationObjectResponse = ImportPreservationObject(request, fileUuid);
                if (importPreservationObjectResponse.ErrorInfo != null && importPreservationObjectResponse.ErrorInfo.ErrorCode != 0)
                {
                    var errorDetails = ToErrorDetails(importPreservationObjectResponse.ErrorInfo);

                    Log.ErrorFormat("Import preservation object failed: {0} (errorId: {1}, errorCode: {2})", errorDetails.ErrorMessage, errorDetails.ErrorId, errorDetails.ErrorCode);
                    Log.Error("----- Import failed ------------------------------");

                    return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, errorDetails.ErrorMessage);
                }

                Log.Debug("Preservation object imported OK");

                // Import metadata
                Log.Debug("----- Importing metadata -------------------------");

                var importMetadataResponse = ImportMetadata(request);
                if (importMetadataResponse.ErrorInfo != null && importMetadataResponse.ErrorInfo.ErrorCode != 0)
                {
                    var errorDetails = ToErrorDetails(importMetadataResponse.ErrorInfo);

                    Log.ErrorFormat("Import metadata failed: {0} (errorId: {1}, errorCode: {2})", errorDetails.ErrorMessage, errorDetails.ErrorId, errorDetails.ErrorCode);
                    Log.Error("----- Import failed ------------------------------");

                    return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, errorDetails.ErrorMessage);
                }

                Log.Debug("Metadata imported OK");

                // Apply import
                Log.Debug("----- Applying import ----------------------------");

                var applyImportResponse = ApplyImport(request, fileUuid);
                if (applyImportResponse.ErrorInfo != null && applyImportResponse.ErrorInfo.ErrorCode != 0)
                {
                    var errorDetails = ToErrorDetails(applyImportResponse.ErrorInfo);

                    Log.ErrorFormat("Apply import failed: {0} (errorId: {1}, errorCode: {2})", errorDetails.ErrorMessage, errorDetails.ErrorId, errorDetails.ErrorCode);
                    Log.Error("----- Import failed ------------------------------");

                    return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, errorDetails.ErrorMessage);
                }

                Log.Debug("Import applied OK");

                Log.Debug("----- Import done --------------------------------");
                Log.DebugFormat("ImportedFileSetId = {0}", applyImportResponse.ImportedFileSetId);

                return Request.CreateResponse(HttpStatusCode.OK, new ImportResponse()
                {
                    ImportedFileSetId = applyImportResponse.ImportedFileSetId
                });

            }
            catch (Exception ex)
            {
                Log.Error("----- Import failed because of an exception ------");
                Log.ErrorFormat("Message: {0}", ex.Message);
                Log.ErrorFormat("Stack trace: {0}", ex.StackTrace);

                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, ex.Message);
            }
        }

        private ImportPreservationObjectResponse ImportPreservationObject(ImportRequest importRequest, string fileUuid)
        {
            var preservationObjectBytes = Base64ToByteArray(importRequest.PreservationObject.Data);

            Log.DebugFormat("Preservation object has {0} bytes", preservationObjectBytes.Length);

            var request = new ImportPreservationObjectRequest
            {
                SubmissionAgreementId = importRequest.SubmissionAgreementId,
                FileSetId = fileUuid,
                FileExtension = importRequest.PreservationObject.FileExtension,
                TotalFileSize = preservationObjectBytes.Length,
                ChunkSize = preservationObjectBytes.Length,
                Chunk = preservationObjectBytes
            };

            return _client.ImportPreservationObject(request);
        }

        private ImportMetadataFileResponse ImportMetadata(ImportRequest importRequest)
        {
            var metadataXmlBytes = Base64ToByteArray(importRequest.MetadataXml);

            Log.DebugFormat("Metadata XML has {0} bytes", metadataXmlBytes.Length);

            var request = new ImportMetadataFileRequest()
            {
                SubmissionAgreementId = importRequest.SubmissionAgreementId,
                FileSetId = importRequest.Uuid,
                Encoding = "UTF-8",
                TotalFileSize = metadataXmlBytes.Length,
                Chunk = metadataXmlBytes,
                ChunkSize = metadataXmlBytes.Length
            };

            return _client.ImportMetadataFile(request);
        }

        private ApplyImportResponse ApplyImport(ImportRequest importRequest, string fileUuid)
        {
            var metadataChecksum = CreateChecksum(Base64ToByteArray(importRequest.MetadataXml));
            var fileChecksum = CreateChecksum(Base64ToByteArray(importRequest.PreservationObject.Data));

            Log.DebugFormat("Metadata checksum ({0}): {1}", metadataChecksum.Algorithm, ToHexString(metadataChecksum.Value));
            Log.DebugFormat("File/preservation object checksum ({0}): {1}", fileChecksum.Algorithm, ToHexString(fileChecksum.Value));

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

            return _client.ApplyImport(request);
        }


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
