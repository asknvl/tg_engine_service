using Amazon.S3;
using Amazon.S3.Model;
using MongoDB.Bson.Serialization.Serializers;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using tg_engine.config;

namespace tg_engine.s3
{
    public class S3Provider : IS3Provider
    {
        #region vars
        settings_s3 settings;
        IAmazonS3 client;
        #endregion

        public S3Provider(settings_s3 settings)
        {
            this.settings = settings;

            client = new AmazonS3Client(

                    settings.user,
                    settings.password,
                    new AmazonS3Config
                    {
                        ServiceURL = $"{settings.host}:{settings.port}",
                        ForcePathStyle = true
                    }
            );
        }

        #region public 
        public async Task<string> Upload(byte[] bytes)
        {
            string key = $"{Guid.NewGuid()}";

            try
            {

                using (var stream = new MemoryStream(bytes))
                {
                    var putRequest = new PutObjectRequest
                    {
                        BucketName = settings.bucket,
                        Key = key,
                        InputStream = stream
                    };

                    var response = await client.PutObjectAsync(putRequest);
                }

                return key;

            } catch (Exception ex)
            {
                throw new Exception($"S3 Upload error: {ex.Message}");
            }
        }

        public async Task<byte[]> Download(string storage_id)
        {
            byte[] res = new byte[0];

            try
            {
                var getRequest = new GetObjectRequest
                {
                    BucketName = settings.bucket,
                    Key = storage_id
                };

                using (var response = await client.GetObjectAsync(getRequest))
                using (var responseStream = response.ResponseStream)
                {
                    using (var memoryStream = new MemoryStream())
                    {
                        await responseStream.CopyToAsync(memoryStream);
                        res = memoryStream.ToArray();        
                    }
                }                

                return res;
                
            } catch (Exception ex)
            {
                throw new Exception($"S3 Download error: {ex.Message}");
            }
        }
        #endregion
    }
}
