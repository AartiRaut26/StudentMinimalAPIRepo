using System.Collections.Generic;

namespace MinimalApiStudent.DTO
{
    public class JsonPatchDocumentDTO
    {
        public List<PatchOperations> Operations { get; set; }

    }
}
