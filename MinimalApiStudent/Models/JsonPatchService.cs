using Microsoft.AspNetCore.JsonPatch;

namespace MinimalApiStudent.Models
{
    public class JsonPatchService
    {

        public void ApplyPatch<TEntity>(TEntity Student, JsonPatchDocument<TEntity> patchDocument) where TEntity : class
        {
            patchDocument.ApplyTo(Student);
        }
    }
}
