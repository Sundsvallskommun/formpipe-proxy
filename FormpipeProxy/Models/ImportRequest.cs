using System;

namespace FormpipeProxy.Models
{
    public class ImportRequest
    {
        public string SubmissionAgreementId { get; set; }

        public string Uuid { get; set; }

        public int ConfidentialityLevel { get; set; }

        public DateTime ConfidentialityDegradationDate  { get; set; }

        public bool PersonalDataFlag { get; set; }

        public string MetadataXml { get; set; }

        public PreservationObject PreservationObject { get; set; }
    }
}