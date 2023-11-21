namespace MinimalApiStudent.Models
{
    public class SieveModel
    {
        public string Filters { get; set; }
        public string Sorts { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;

        internal IQueryable<Student> Apply(IQueryable<Student> query)
        {
            throw new NotImplementedException();
        }
    }
}
