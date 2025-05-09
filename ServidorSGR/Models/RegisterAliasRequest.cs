// ServidorSGR/Models/RegisterAliasRequest.cs
using System.ComponentModel.DataAnnotations;
using MessagePack;

namespace ServidorSGR.Models
{
    [MessagePackObject] 
    public class RegisterAliasRequest
    {
        [MessagePack.Key(0)] 
        public string Alias { get; set; } = string.Empty; 
    }
}