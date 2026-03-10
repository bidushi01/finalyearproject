namespace finalyearproject.Data.Models.Domain
{
    public class UserSkill
    {
        public string FieldName { get; set; }
        public string SkillName { get; set; }
        public string SubSkillName { get; set; }
        public int SubSkillId { get; set; }
        public int ExperienceLevel { get; set; }
        public string AvailableDays { get; set; }
        public string AvailableTimeStart { get; set; }
        public string AvailableTimeEnd { get; set; }
    }
}
