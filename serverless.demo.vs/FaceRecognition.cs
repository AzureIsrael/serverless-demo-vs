// Setup
// 1) Go to https://www.microsoft.com/cognitive-services/en-us/computer-vision-api 
//    Sign up for computer vision api
// 2) Go to Platform features -> Application settings
//    create a new app setting Vision_API_Subscription_Key and use Computer vision key as value

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

namespace serverless.demo.vs
{
    public static class FaceRecognition
    {
        [FunctionName("FaceRecognition")]
        public static async Task Run(
            [CosmosDBTrigger("serverlessdemodb",
            "images",
            ConnectionStringSetting = "CosmosDBDocConnectionString",
            LeaseCollectionName = "faceRecognitionLeases",
            CreateLeaseCollectionIfNotExists = true,
            LeaseConnectionStringSetting = "CosmosDBDocConnectionString",
            LeaseDatabaseName = "serverlessdemodb",
            LeasesCollectionThroughput = 10000)]
            IReadOnlyList<Document> modifiedDocuments,
            TraceWriter log,
            [DocumentDB("serverlessdemodb", "images", ConnectionStringSetting = "CosmosDBDocConnectionString")] IAsyncCollector<Document> documents)
        {
            try
            {
                foreach (var modifiedDocument in modifiedDocuments)
                {
                    log.Info($"loading document [{modifiedDocument.Id}]");
                    Face[] faces = modifiedDocument.GetPropertyValue<Face[]>("faces");
                    if (faces != null)
                    {
                        log.Info($"already proccesed document [{modifiedDocument.Id}], skipping...");
                        return;
                    }

                    CloudBlockBlob blockBlob = Helper.GetBlockBlob(modifiedDocument);


                    using (Stream stream = blockBlob.OpenRead())
                    {
                        FaceServiceClient client = new FaceServiceClient(Environment.GetEnvironmentVariable("CognitiveFaceApiKey"), Environment.GetEnvironmentVariable("CognitiveFaceApiUrl"));
                        var requiredFaceAttributes = new FaceAttributeType[] {
                        FaceAttributeType.Age,
                        FaceAttributeType.Gender,
                        FaceAttributeType.Smile,
                        FaceAttributeType.FacialHair,
                        FaceAttributeType.HeadPose,
                        FaceAttributeType.Glasses
                    };
                        try
                        {
                            faces = await client.DetectAsync(blockBlob.Uri.AbsoluteUri, true, false, requiredFaceAttributes);

                            if (faces == null || faces.Length == 0)
                            {
                                log.Info($"no faces were detected in image [{blockBlob.Uri.AbsoluteUri}] connected to document [{modifiedDocument.Id}]");
                                return;
                            }

                            log.Info($"found {faces.Length} faces were detected in image [{blockBlob.Uri.AbsoluteUri}] updating document [{modifiedDocument.Id}]");
                            Document document = modifiedDocument;
                            document.SetPropertyValue("faces", faces);
                            await documents.AddAsync(document);
                        }
                        catch (Exception ex)
                        {
                            log.Error("failed detecting faces", ex);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                log.Error("face recognition funciton failed", ex);
            }
        }       
    }
}
