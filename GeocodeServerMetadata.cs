using System.Collections.Generic;

namespace GeoREST
{

    public class SingleLineAddressField
    {
        public string name { get; set; }
        public string type { get; set; }
        public string alias { get; set; }
        public bool required { get; set; }
        public int length { get; set; }
        //public LocalizedNames localizedNames { get; set; }
        //public RecognizedNames recognizedNames { get; set; }
    }

    public class AddressField
    {
      public string name { get; set; }
      public string type { get; set; }
      public string alias { get; set; }
      public bool required { get; set; }
      public int length { get; set; }
    }

    public class CandidateField
    {
        public string name { get; set; }
        public string type { get; set; }
        public string alias { get; set; }
        public bool required { get; set; }
        public int length { get; set; }
    }

    public class SpatialReference
    {
        public int wkid { get; set; }
        public int latestWkid { get; set; }
    }

    public class LocatorProperties
    {
        public string UICLSID { get; set; }
        public string IntersectionConnectors { get; set; }
        public int SuggestedBatchSize { get; set; }
        public int MaxBatchSize { get; set; }
        public int LoadBalancerTimeOut { get; set; }
        public string WriteXYCoordFields { get; set; }
        public string WriteStandardizedAddressField { get; set; }
        public string WriteReferenceIDField { get; set; }
        public string WritePercentAlongField { get; set; }
    }

    public class GeocodeServerMetadata
    {
        public double currentVersion { get; set; }
        public string serviceDescription { get; set; }
        public List<AddressField> addressFields { get; set; }
        public SingleLineAddressField singleLineAddressField { get; set; }
        public List<CandidateField> candidateFields { get; set; }
        public SpatialReference spatialReference { get; set; }
        public LocatorProperties locatorProperties { get; set; }
        public List<string> countries { get; set; }
        public string capabilities { get; set; }

        public GeocodeServerMetadata()
        {
            this.currentVersion = 10.5;
            this.serviceDescription = "NYC Geoclient";
            this.singleLineAddressField = new SingleLineAddressField();
            this.singleLineAddressField.name = "SingleLine";
            this.singleLineAddressField.type = "esriFieldTypeString";
            this.singleLineAddressField.alias = "Single Line Input";
            this.singleLineAddressField.required = false;
            this.singleLineAddressField.length = 100;

            this.spatialReference = new SpatialReference() { wkid = 4326, latestWkid = 4326 };
            this.countries = new List<string>();
            this.countries.Add("US");

            this.addressFields = new List<AddressField>();
            this.addressFields.Add(new AddressField() { alias = "Place", required = true, length = 100, name = "Place", type = "esriFieldTypeString" });
            this.addressFields.Add(new AddressField() { alias = "Address", required = true, length = 100, name = "Address", type = "esriFieldTypeString" });
            this.addressFields.Add(new AddressField() { alias = "Borough", required = true, length = 16, name = "Borough", type = "esriFieldTypeString" });
            this.addressFields.Add(new AddressField() { alias = "BBL", required = true, length = 20, name = "BBL", type = "esriFieldTypeString" });
            this.addressFields.Add(new AddressField() { alias = "BIN", required = true, length = 20, name = "BIN", type = "esriFieldTypeString" });
            this.addressFields.Add(new AddressField() { alias = "BlockFace", required = true, length = 40, name = "BBL", type = "esriFieldTypeString" });
            this.addressFields.Add(new AddressField() { alias = "Intersection", required = true, length = 100, name = "Intersection", type = "esriFieldTypeString" });

            this.candidateFields = new List<CandidateField>();
            this.candidateFields.Add(new CandidateField() { name = "Loc_name", type = "esrifieldTypeString", alias = "Loc_name", required = false, length = 24 });
            this.candidateFields.Add(new CandidateField() { name = "Match_addr", type = "esrifieldTypeString", alias = "Match_addr", required = false, length = 50 });
            this.candidateFields.Add(new CandidateField() { name = "Addr_type", type = "esrifieldTypeString", alias = " Addr_type", required = false, length = 24 });
            this.candidateFields.Add(new CandidateField() { name = "bbl", type = "esrifieldTypeString", alias = "bbl", required = false, length = 20 });
            this.candidateFields.Add(new CandidateField() { name = "bblTaxBlock", type = "esrifieldTypeString", alias = "bblTaxBlock", required = false, length = 10 });
            this.candidateFields.Add(new CandidateField() { name = "bblTaxLot", type = "esrifieldTypeString", alias = "bblTaxLot", required = false, length = 10 });
            this.candidateFields.Add(new CandidateField() { name = "bblBoroughCode", type = "esrifieldTypeString", alias = "bblBoroughCode", required = false, length = 5 });
            this.candidateFields.Add(new CandidateField() { name = "houseNumber", type = "esrifieldTypeString", alias = "houseNumber", required = false, length = 12 });
            this.candidateFields.Add(new CandidateField() { name = "firstStreetNameNormalized", type = "esrifieldTypeString", alias = "firstStreetNameNormalized", required = false, length = 24 });
            this.candidateFields.Add(new CandidateField() { name = "secondStreetNameNormalized", type = "esrifieldTypeString", alias = "secondStreetNameNormalized", required = false, length = 24 });
            this.candidateFields.Add(new CandidateField() { name = "buildingIdentificationNumber", type = "esrifieldTypeString", alias = "buildingIdentificationNumber", required = false, length = 20 });
            this.candidateFields.Add(new CandidateField() { name = "firstBoroughName", type = "esrifieldTypeString", alias = "firstBoroughName", required = false, length = 16 });

            this.locatorProperties = new LocatorProperties();
            this.locatorProperties.IntersectionConnectors = "&amp; @ | and";
            this.locatorProperties.SuggestedBatchSize = 150;
            this.locatorProperties.MaxBatchSize = 500;
            this.locatorProperties.LoadBalancerTimeOut = 60;
            this.locatorProperties.WriteXYCoordFields = "TRUE";
            this.locatorProperties.WriteStandardizedAddressField = "FALSE";
            this.locatorProperties.WriteReferenceIDField = "FALSE";
            this.locatorProperties.WritePercentAlongField = "FALSE";
            this.locatorProperties.UICLSID = "1234";

            this.countries.Add("US");
            this.capabilities = "Geocode,BatchGeocode";
        }
    }
}