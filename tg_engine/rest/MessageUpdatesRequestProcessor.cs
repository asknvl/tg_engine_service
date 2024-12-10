using HttpMultipartParser;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using tg_engine.interlayer.messaging;
using tg_engine.rest.updates;
using System.Web;
using System.Collections.Specialized;
using TL;

namespace tg_engine.rest
{
    public class MessageUpdatesRequestProcessor : IRequestProcessor, IMessageUpdatesObservable
    {
        #region vars
        List<IMessageUpdatesObserver> messageUpdatesObservers = new List<IMessageUpdatesObserver>();
        #endregion

        public MessageUpdatesRequestProcessor() {            
        }

        #region dtos  
        public class messageDto
        {
            public Guid account_id { get; set; }
            public Guid chat_id { get; set; }
            public Guid telegram_user_id { get; set; }
            public string? operator_id { get; set; }
            public string? operator_letters { get; set; }
            public int? reply_to_message_id { get; set; } = null;
            public mediaDto? media { get; set; } 
            public string? text { get; set; }
            public string? screen_text { get; set; }
            public bool? is_scheduled { get; set; }
            public DateTime? scheduled_date { get; set; }
        }

        public class mediaDto
        {
            public string type { get; set; }      
            public string? file_name { get; set; }
            public string storage_id { get; set; }            
        }      

        public class clippedDto
        {
            public Guid account_id { get; set; }
            public Guid chat_id { get; set; }
            public Guid telegram_user_id { get; set; }
            public string? operator_id { get; set; }
            public string? operator_letters { get; set; }
            public bool? is_scheduled { get; set; }
            public DateTime? scheduled_date { get; set; }
            public int? reply_to_message_id { get; set; } = null;
            public string? text { get; set; } = null;
            public string? screen_text { get; set; } = null;
            public string type { get; set; }
            public string file_name { get; set; }
            public string file_extension { get; set; }
            public string? file_hash { get; set; } = null;
            public byte[] file { get; set; }                       
        }
        #endregion

        #region helpers   

        string getExtensionFromMimeType(string input)
        {
            var res = input;
            var index = input.IndexOf('/');
            if (index >= 0)
                res = input.Substring(index + 1);
            return res;
        }

        string getTypeFromMimeType(string input)
        {
            var res = input;
            var index = input.IndexOf('/');
            res = res.Remove(index);
            return res;
        }
        #endregion

        #region public
        public void Add(IMessageUpdatesObserver observer)
        {
            if (observer != null && !messageUpdatesObservers.Contains(observer))
                messageUpdatesObservers.Add(observer);
        }
        public void Remove(IMessageUpdatesObserver observer)
        {
            messageUpdatesObservers.Remove(observer);
        }

        public async Task<(HttpStatusCode, string)> ProcessGetRequest(string[] splt_route, NameValueCollection? query = null)
        {
            var code = HttpStatusCode.NotFound;
            var responseText = code.ToString();
            IMessageUpdatesObserver? observer = null;

            try
            {
                var updReq = splt_route[2];

                switch (updReq)
                {
                    case "get-scheduled":
                        try
                        {                            
                            Guid account_id = Guid.Empty;
                            Guid chat_id = Guid.Empty;

                            var qAccId = query.Get("account_id");                            

                            if (qAccId != null && !Guid.TryParse(qAccId, out account_id))
                            {
                                throw new ArgumentException("Illegal account_id");
                            }

                            observer = messageUpdatesObservers.FirstOrDefault(o => o.account_id == account_id);
                            if (observer != null)
                            {
                                var qChtId = query.Get("chat_id");
                                if (qChtId != null && !Guid.TryParse(qChtId, out chat_id))
                                {
                                    throw new ArgumentException("Illegal chat_id");
                                }

                                var update = new getScheduled()
                                {
                                    account_id = account_id,
                                    chat_id = chat_id,
                                };

                                var response = await observer.OnUpdateRequest(update);
                                responseText = JsonConvert.SerializeObject(response, Formatting.Indented);   
                                code = HttpStatusCode.OK;
                            }
                        }                        
                        catch (ArgumentException ex)
                        {
                            code = HttpStatusCode.BadRequest;
                            responseText = $"{updReq}: {ex.Message}";
                        }
                        catch (Exception ex)
                        {
                            code = HttpStatusCode.InternalServerError;
                            responseText = $"{updReq}: {ex.Message}";
                        }

                        break;

                    default:
                        break;
                }

            } catch (Exception ex)
            {

            }
            
            return (code, responseText);
        }

        public async Task<(HttpStatusCode, string)> ProcessPostRequest(string[] splt_route, HttpListenerRequest request)
        {
            var code = HttpStatusCode.NotFound;
            var responseText = code.ToString();

            IMessageUpdatesObserver? observer = null;            

            try
            {
                var updReq = splt_route[2];

                switch (updReq)
                {
                    case "send-message":
                        try
                        {
                            using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
                            var data = await reader.ReadToEndAsync();                            

                            var message = JsonConvert.DeserializeObject<messageDto>(data);
                            observer = messageUpdatesObservers.FirstOrDefault(o => o.account_id == message.account_id);
                            if (observer != null)
                            {
                                observer.OnNewMessage(message);

                                code = HttpStatusCode.OK;
                                responseText = code.ToString();
                            }

                        } catch (Exception ex)
                        {
                            code = HttpStatusCode.BadRequest;
                            responseText = $"{updReq}: {ex.Message}";
                        }
                        break;

                    case "send-clipped":
                        try
                        {
                            clippedDto clippedDto = new clippedDto();                            

                            using (var memoryStream = new MemoryStream())
                            {
                                await request.InputStream.CopyToAsync(memoryStream);
                                memoryStream.Position = 0;

                                var parser = await MultipartFormDataParser.ParseAsync(memoryStream);

                                var account_id = parser.GetParameterValue("account_id");
                                if (string.IsNullOrEmpty(account_id) || !Guid.TryParse(account_id, out var _account_id))
                                    throw new Exception("Illegal account_id");
                                clippedDto.account_id = _account_id;


                                var chat_id = parser.GetParameterValue("chat_id");
                                if (string.IsNullOrEmpty(chat_id) || !Guid.TryParse(chat_id, out var _chat_id))
                                    throw new Exception("Illegal chat_id");
                                clippedDto.chat_id = _chat_id;

                                var telegram_user_id = parser.GetParameterValue("telegram_user_id");
                                if (string.IsNullOrEmpty(telegram_user_id) || !Guid.TryParse(telegram_user_id, out var _telegram_user_id))
                                    throw new Exception("Illegal telegram_user_id");
                                clippedDto.telegram_user_id = _telegram_user_id;

                                var reply_to_message_id = parser.GetParameterValue("reply_to_message_id");
                                if (int.TryParse(reply_to_message_id, out var _reply_to_message_id))
                                    clippedDto.reply_to_message_id = _reply_to_message_id;

                                var text = parser.GetParameterValue("text");
                                clippedDto.text = text;

                                var screen_text = parser.GetParameterValue("screen_text");
                                clippedDto.screen_text = screen_text;

                                
                                //var allowed = new string[] { MediaTypes.video, MediaTypes.image, MediaTypes.photo };
                                var type = parser.GetParameterValue("type");
                                if (string.IsNullOrEmpty(type) || !MediaTypes.IsMediaType(type) /*!allowed.Contains(type)*/)
                                    throw new Exception($"Illegal type {type}");
                                clippedDto.type = type; 

                                var operator_id = parser.GetParameterValue("operator_id");                                
                                clippedDto.operator_id = operator_id;

                                var operator_letters = parser.GetParameterValue("operator_letters");
                                clippedDto.operator_letters = operator_letters;

                                var is_scheduled = parser.GetParameterValue("is_scheduled");
                                clippedDto.is_scheduled = is_scheduled.ToLower().Equals("true");

                                if (clippedDto.is_scheduled == true)
                                {
                                    var scheduled_date = parser.GetParameterValue("scheduled_date");
                                    if (DateTime.TryParse(scheduled_date, out var date))
                                    {
                                        clippedDto.scheduled_date = date;   
                                    }
                                }

                                if (parser.Files.Count > 0)
                                {
                                    var file = parser.Files.First();
                                    if (file != null && file.Data != null && file.Data.Length > 0)
                                    {

                                        clippedDto.file_name = file.FileName;
                                        clippedDto.file_extension = getExtensionFromMimeType(file.ContentType);

                                        Stream data = file.Data;
                                        using (var ms = new MemoryStream())
                                        {
                                            data.CopyTo(ms);
                                            clippedDto.file = ms.ToArray();
                                        }
                                    }
                                }                                
                                //else
                                //{
                                //    throw new Exception("No file");
                                //}

                                observer = messageUpdatesObservers.FirstOrDefault(o => o.account_id == clippedDto.account_id);
                                if (observer != null)
                                {
                                    observer.OnNewMessage(clippedDto);

                                    code = HttpStatusCode.OK;
                                    responseText = code.ToString();
                                }
                            }


                        }
                        catch (Exception ex)
                        {
                            code = HttpStatusCode.BadRequest;
                            responseText = $"{updReq}: {ex.Message}";
                        }
                        break;

                    case "edit-message":
                        try
                        {
                            editMessage editMessage = new editMessage();

                            using (var memoryStream = new MemoryStream())
                            {
                                await request.InputStream.CopyToAsync(memoryStream);
                                memoryStream.Position = 0;
                                var parser = await MultipartFormDataParser.ParseAsync(memoryStream);

                                var account_id = parser.GetParameterValue("account_id");
                                if (string.IsNullOrEmpty(account_id) || !Guid.TryParse(account_id, out var _account_id))
                                    throw new Exception("Illegal account_id");
                                editMessage.account_id = _account_id;


                                var chat_id = parser.GetParameterValue("chat_id");
                                if (string.IsNullOrEmpty(chat_id) || !Guid.TryParse(chat_id, out var _chat_id))
                                    throw new Exception("Illegal chat_id");
                                editMessage.chat_id = _chat_id;

                                var telegram_user_id = parser.GetParameterValue("telegram_user_id");
                                if (string.IsNullOrEmpty(telegram_user_id) || !Guid.TryParse(telegram_user_id, out var _telegram_user_id))
                                    throw new Exception("Illegal telegram_user_id");
                                editMessage.telegram_user_id = _telegram_user_id;

                                var telegram_message_id = parser.GetParameterValue("telegram_message_id");
                                if (int.TryParse(telegram_message_id, out var _telegram_message_id))
                                    editMessage.telegram_message_id = _telegram_message_id;
                                else throw new Exception("Illegal telegram_message_id");

                                var text = parser.GetParameterValue("text");
                                editMessage.text = text;

                                var screen_text = parser.GetParameterValue("screen_text");
                                editMessage.screen_text = screen_text;

                                var operator_id = parser.GetParameterValue("operator_id");
                                editMessage.operator_id = operator_id;

                                var operator_letters = parser.GetParameterValue("operator_letters");
                                editMessage.operator_letters = operator_letters;

                                if (parser.Files.Count > 0 && parser.Files[0].Data.Length > 0)
                                {
                                    var file = parser.Files.First();
                                    editMessage.file_name = file.FileName;
                                    editMessage.file_extension = getExtensionFromMimeType(file.ContentType);
                                    
                                    switch (getTypeFromMimeType(file.ContentType))
                                    {
                                        case "video":
                                            editMessage.type = MediaTypes.video;
                                            break;

                                        case "image":
                                            editMessage.type = MediaTypes.image;
                                            break;

                                        default:
                                            break;
                                    }

                                    Stream data = file.Data;
                                    using (var ms = new MemoryStream())
                                    {
                                        data.CopyTo(ms);
                                        editMessage.file = ms.ToArray();
                                    }
                                }

                                observer = messageUpdatesObservers.FirstOrDefault(o => o.account_id == editMessage.account_id);
                                if (observer != null)
                                {
                                    observer.OnNewUpdate(editMessage);

                                    code = HttpStatusCode.OK;
                                    responseText = code.ToString();
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            code = HttpStatusCode.BadRequest;
                            responseText = $"{updReq}: {ex.Message}";
                        }
                        break;

                    case "read-history":
                        try
                        {
                            using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
                            var data = await reader.ReadToEndAsync();
                            var update = JsonConvert.DeserializeObject<readHistory>(data);

                            observer = messageUpdatesObservers.FirstOrDefault(o => o.account_id == update.account_id);
                            if (observer != null)
                            {
                                await observer.OnNewUpdate(update);
                                code = HttpStatusCode.OK;
                                responseText = code.ToString();
                            }

                        } catch (Exception ex)
                        {
                            code = HttpStatusCode.BadRequest;
                            responseText = $"{updReq}: {ex.Message}";
                        }
                        break;

                    case "delete-messages":
                        try
                        {
                            using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
                            var data = await reader.ReadToEndAsync();
                            var update = JsonConvert.DeserializeObject<deleteMessage>(data);

                            observer = messageUpdatesObservers.FirstOrDefault(o => o.account_id == update.account_id);
                            if (observer != null)
                            {
                                await observer.OnNewUpdate(update);
                                code = HttpStatusCode.OK;
                                responseText = code.ToString();
                            }
                        } catch (Exception ex)
                        {
                            code = HttpStatusCode.BadRequest;
                            responseText = $"{updReq}: {ex.Message}";
                        }
                        break;

                    //case "ai-status":
                    //    try
                    //    {
                    //        using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
                    //        var data = await reader.ReadToEndAsync();
                    //        var update = JsonConvert.DeserializeObject<aiStatus>(data);

                    //        observer = messageUpdatesObservers.FirstOrDefault(o => o.account_id == update.account_id);

                    //        if (observer != null)
                    //        {
                    //            await observer.OnNewUpdate(update);
                    //            code = HttpStatusCode.OK;
                    //            responseText = code.ToString();
                    //        }

                    //    } catch (Exception ex)
                    //    {
                    //        code = HttpStatusCode.BadRequest;
                    //        responseText = $"{updReq}: {ex.Message}";
                    //    }
                    //    break;

                    case "ai-status":
                        try
                        {
                            using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
                            var data = await reader.ReadToEndAsync();
                            var update = JsonConvert.DeserializeObject<aiStatus>(data);

                            observer = messageUpdatesObservers.FirstOrDefault(o => o.account_id == update.account_id);

                            if (observer != null)
                            {
                                await observer.OnNewUpdate(update);
                                code = HttpStatusCode.OK;
                                responseText = code.ToString();
                            }

                        }
                        catch (Exception ex)
                        {
                            code = HttpStatusCode.BadRequest;
                            responseText = $"{updReq}: {ex.Message}";
                        }
                        break;

                    case "send-typing":
                        try
                        {
                            using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
                            var data = await reader.ReadToEndAsync();
                            var update = JsonConvert.DeserializeObject<sendTyping>(data);

                            observer = messageUpdatesObservers.FirstOrDefault(o => o.account_id == update.account_id);

                            if (observer != null)
                            {
                                await observer.OnNewUpdate(update);
                                code = HttpStatusCode.OK;
                                responseText = code.ToString();
                            }

                        } catch (Exception ex)
                        {
                            code = HttpStatusCode.BadRequest;
                            responseText = $"{updReq}: {ex.Message}";
                        }
                        break;

                    case "send-status":
                        try
                        {
                            using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
                            var data = await reader.ReadToEndAsync();
                            var update = JsonConvert.DeserializeObject<sendStatus>(data);

                            observer = messageUpdatesObservers.FirstOrDefault(o => o.account_id == update.account_id);

                            if (observer != null)
                            {
                                await observer.OnNewUpdate(update);
                                code = HttpStatusCode.OK;
                                responseText = code.ToString();
                            }

                        }
                        catch (Exception ex)
                        {
                            code = HttpStatusCode.BadRequest;
                            responseText = $"{updReq}: {ex.Message}";
                        }
                        break;

                    case "send-reaction":
                        try
                        {
                            using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
                            var data = await reader.ReadToEndAsync();
                            var update = JsonConvert.DeserializeObject<sendReaction>(data);

                            observer = messageUpdatesObservers.FirstOrDefault(o => o.account_id == update.account_id);

                            if (observer != null)
                            {
                                await observer.OnNewUpdate(update);
                                code = HttpStatusCode.OK;
                                responseText = code.ToString();
                            }

                        }
                        catch (Exception ex)
                        {
                            code = HttpStatusCode.BadRequest;
                            responseText = $"{updReq}: {ex.Message}";
                        }
                        break;

                    case "delete-scheduled":
                        try
                        {
                            using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
                            var data = await reader.ReadToEndAsync();
                            var update = JsonConvert.DeserializeObject<deleteScheduled>(data);

                            observer = messageUpdatesObservers.FirstOrDefault(o => o.account_id == update.account_id);
                            if (observer != null)
                            {
                                await observer.OnNewUpdate(update);
                                code = HttpStatusCode.OK;
                                responseText = code.ToString();
                            }
                        }
                        catch (Exception ex)
                        {
                            code = HttpStatusCode.BadRequest;
                            responseText = $"{updReq}: {ex.Message}";
                        }
                        break;

                    default:
                        break;
                }
            }
            catch (Exception ex)
            {
                responseText = $"{code} {ex.Message}";
            }

            await Task.CompletedTask;
            return (code, responseText);
        }      
        #endregion
    }    
}
