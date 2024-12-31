using RimWorld;
using Verse.AI;

namespace AutoToolSwitcher
{
    public class SkillJob
    {
        public SkillJob(SkillDef skill, Job job)
        {
            this.skill = skill;
            this.job = job;
        }

        public SkillDef skill;
        public Job job;
    }
}
