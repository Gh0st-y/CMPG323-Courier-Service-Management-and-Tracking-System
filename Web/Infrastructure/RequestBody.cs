using System.IO;
using System.Web;
using Newtonsoft.Json;

namespace CourierService.Web.Infrastructure
{
    public static class RequestBody
    {
        /// <summary>
        /// Reads a JSON request body. Returns null when there is no body at all. When there is a body that isn't
        /// valid JSON for <typeparamref name="T"/>, returns null and sets <paramref name="malformed"/>, so the
        /// caller can answer 400 instead of silently treating it as empty.
        /// </summary>
        public static T Read<T>(HttpRequestBase request, out bool malformed) where T : class
        {
            malformed = false;

            if (request.InputStream.CanSeek)
            {
                request.InputStream.Position = 0;
            }

            using (var reader = new StreamReader(request.InputStream))
            {
                var body = reader.ReadToEnd();
                if (string.IsNullOrWhiteSpace(body))
                {
                    return null;
                }

                try
                {
                    return JsonConvert.DeserializeObject<T>(body);
                }
                catch (JsonException)
                {
                    malformed = true;
                    return null;
                }
            }
        }
    }
}
