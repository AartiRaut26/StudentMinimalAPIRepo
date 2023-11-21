using System.Collections.Generic;

namespace MinimalApiStudent.DTO
{
    public class PatchOperations
    {
        public string Op { get; set; }
        public string Path { get; set; }
        public string Value { get; set; }
    }
}
