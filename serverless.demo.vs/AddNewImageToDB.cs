using System.IO;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;

namespace serverless.demo.vs
{
    public static class AddNewImageToDB
    {
        [FunctionName("AddNewImageToDB")]
        public static void Run(
            [BlobTrigger("images/{name}", Connection = "ImagesStorageConnectionString")]Stream myBlob, 
            string name, 
            TraceWriter log, 
            [DocumentDB("serverlessdemodb", "images", ConnectionStringSetting = "CosmosDBDocConnectionString")] out dynamic document)
        {
            log.Info($"C# Blob trigger function Processed blob\n Name:{name} \n Size: {myBlob.Length} Bytes");
            document = new { fileName = name, fileSize = myBlob.Length };
        }
    }
}
