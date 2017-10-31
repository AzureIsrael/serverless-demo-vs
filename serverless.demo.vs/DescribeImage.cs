using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Microsoft.Azure.Documents;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Newtonsoft.Json;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage;
using Microsoft.ProjectOxford.Face;
using Microsoft.ProjectOxford.Face.Contract;
using Microsoft.ProjectOxford.Vision;
using Microsoft.ProjectOxford.Vision.Contract;

namespace serverless.demo.vs
{
    public static class DescribeImage
    {
        [FunctionName("DescribeImage")]
        public static async Task Run(
            [CosmosDBTrigger("serverlessdemodb",
            "images",
            ConnectionStringSetting = "CosmosDBDocConnectionString",
            LeaseCollectionName = "describeImageLeases",
            CreateLeaseCollectionIfNotExists = true,
            LeaseConnectionStringSetting = "CosmosDBDocConnectionString",
            LeaseDatabaseName = "serverlessdemodb",
            LeasesCollectionThroughput = 10000)]
            IReadOnlyList<Document> modifiedDocuments,
            TraceWriter log,
            [DocumentDB("serverlessdemodb", "images", ConnectionStringSetting = "CosmosDBDocConnectionString")] IAsyncCollector<Document> documents)
        {
            try {
                foreach (var modifiedDocument in modifiedDocuments)
                {
                    log.Info($"loading document [{modifiedDocument.Id}]");
                    AnalysisResult analysisResult = modifiedDocument.GetPropertyValue<AnalysisResult>("analysis");
                    if (analysisResult != null)
                    {
                        log.Info($"already proccesed document [{modifiedDocument.Id}], skipping...");
                        return;
                    }

                    CloudBlockBlob blockBlob = Helper.GetBlockBlob(modifiedDocument);


                    using (Stream stream = blockBlob.OpenRead())
                    {

                        VisionServiceClient client = new VisionServiceClient(Environment.GetEnvironmentVariable("CognitiveVisionApiKey"), Environment.GetEnvironmentVariable("CognitiveVisionApiUrl"));
                        var requiredVisualFeature = new VisualFeature[] {
                        VisualFeature.ImageType,
                        VisualFeature.Color,
                        VisualFeature.Faces,
                        VisualFeature.Adult,
                        VisualFeature.Categories,
                        VisualFeature.Tags,
                        VisualFeature.Description
                    };

                        var requiredDetails = new string[] { "Celebrities", "Landmarks" };
                        try
                        {
                            analysisResult = await client.AnalyzeImageAsync(blockBlob.Uri.AbsoluteUri, requiredVisualFeature, requiredDetails);

                            if (analysisResult == null)
                            {
                                log.Info($"empty analysis result for [{blockBlob.Uri.AbsoluteUri}] connected to document [{modifiedDocument.Id}]");
                                return;
                            }
                            log.Info($"description [{analysisResult.Description?.Captions?[0]?.Text}] for image [{blockBlob.Uri.AbsoluteUri}], updating document [{modifiedDocument.Id}]");
                            Document document = modifiedDocument;
                            document.SetPropertyValue("analysis", analysisResult);
                            await documents.AddAsync(document);
                        }
                        catch (Exception ex)
                        {
                            log.Error("failed analysing image", ex);
                        }
                    }
                }

            }
            catch(Exception ex)
            {
                log.Error("descibe image function failed", ex);
            }


        }

    }
}
