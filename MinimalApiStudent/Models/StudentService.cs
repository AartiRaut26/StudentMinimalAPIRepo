using Microsoft.EntityFrameworkCore;
using Sieve.Services;

using Sieve.Models;
using System.Linq;

namespace MinimalApiStudent.Models
{
   

    public class StudentService
    {
        private readonly StudentAPIDBcontext _dbContext;
        private readonly ISieveProcessor _sieveProcessor;

        public StudentService(StudentAPIDBcontext dbContext, ISieveProcessor sieveProcessor)
        {
            _dbContext = dbContext;
            _sieveProcessor = sieveProcessor;
        }

        public async Task<(int TotalCount, List<Student> Students)> GetFilteredAndSortedStudentsAsync(Sieve.Models.SieveModel sieveModel)
        {
            var query = _dbContext.Students.AsQueryable();

            if (!string.IsNullOrWhiteSpace(sieveModel.Filters))
            {
                query = _sieveProcessor.Apply(sieveModel, query);
            }

            var totalCount = await query.CountAsync();

            int pageSize = sieveModel.PageSize ?? 10; // Use a default value (e.g., 10) if PageSize is null
            var skip = (sieveModel.Page - 1) * pageSize;
            var students = await query.Skip((int)skip).Take(pageSize).ToListAsync();

            return (totalCount, students);
        }
    }


}
