using finalyearproject.Data.Repository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace finalyearproject.UI.Controllers
{
    /// <summary>
    /// Lightweight controller used only for AJAX/modal endpoints on the Admin side.
    /// Keeps modal-specific JSON separate from the main AdminController.
    /// </summary>
    [Authorize(Roles = "Admin")]
    public class AdminModalController : Controller
    {
        private readonly IUserRepository _userRepository;

        public AdminModalController(IUserRepository userRepository)
        {
            _userRepository = userRepository;
        }

        /// <summary>
        /// Returns full skills portfolio for a single user.
        /// Used by the "View" button in Pending Users grid modal.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> UserSkills(int userId)
        {
            var skills = await _userRepository.GetUserSkillsDisplayAsync(userId, approvedOnly: false);
            return Json(skills);
        }

        /// <summary>
        /// Admin action: permanently remove a specific user skill row.
        /// Used from the Skills Portfolio modal grid.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> RemoveUserSkill(int userSkillId)
        {
            if (userSkillId <= 0)
                return Json(new { success = false, message = "Invalid skill id." });

            await _userRepository.DeleteUserSkillAsync(userSkillId);
            return Json(new { success = true });
        }

        /// <summary>
        /// Returns enriched change details: user name, action, when, skill names or CV/portfolio info.
        /// Used by the "View" button in Recent user skill / CV changes grid.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ChangeDetails(int userId, string actionType, int fieldId, int skillId, int subSkillId)
        {
            var user = await _userRepository.GetUserByIdAsync(userId);
            var name = user?.Username ?? ("User #" + userId);

            string fieldName = null, skillName = null, subSkillName = null;
            if ((actionType == "ADD_OR_UPDATE_SKILL" || actionType == "UPDATE_SKILL") && fieldId > 0)
            {
                var fields = await _userRepository.GetAllFieldsAsync();
                var field = ((IEnumerable<dynamic>)fields)?.FirstOrDefault(f => (int)(f.FieldId ?? f.fieldId ?? 0) == fieldId);
                fieldName = field?.FieldName?.ToString() ?? field?.fieldName?.ToString();

                if (skillId > 0 && !string.IsNullOrEmpty(fieldName))
                {
                    var skills = await _userRepository.GetSkillsByFieldAsync(fieldId);
                    var sk = ((IEnumerable<dynamic>)skills)?.FirstOrDefault(s => (int)(s.SkillId ?? s.skillId ?? 0) == skillId);
                    skillName = sk?.SkillName?.ToString() ?? sk?.skillName?.ToString();

                    if (subSkillId > 0 && !string.IsNullOrEmpty(skillName))
                    {
                        var subSkills = await _userRepository.GetSubSkillsBySkillAsync(skillId);
                        var ss = ((IEnumerable<dynamic>)subSkills)?.FirstOrDefault(s => (int)(s.SubSkillId ?? s.subSkillId ?? 0) == subSkillId);
                        subSkillName = ss?.SubSkillName?.ToString() ?? ss?.subSkillName?.ToString();
                    }
                }
            }

            return Json(new
            {
                username = name,
                actionType = actionType ?? "",
                cvPath = user?.CVPath ?? "",
                portfolioUrl = user?.PortfolioUrl ?? "",
                fieldName = fieldName ?? "",
                skillName = skillName ?? "",
                subSkillName = subSkillName ?? ""
            });
        }

        /// <summary>
        /// Returns all recent changes for one user, with skill names resolved.
        /// Used by the popup when View is clicked on a grouped user row.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> UserChangeDetails(int userId)
        {
            var user = await _userRepository.GetUserByIdAsync(userId);
            if (user == null)
                return Json(new { username = "Unknown", changes = new List<object>() });

            var allChanges = await _userRepository.GetRecentUserSkillChangesAsync(100);
            var userChanges = new List<object>();

            if (allChanges != null)
            {
                foreach (dynamic row in allChanges)
                {
                    int uid = Convert.ToInt32(row.UserId ?? row.userId ?? 0);
                    if (uid != userId) continue;

                    string actionType = (string)(row.ActionType ?? row.actionType ?? "");
                    var changedAt = row.ChangedAt ?? row.changedAt;
                    int fieldId = Convert.ToInt32(row.FieldId ?? row.fieldId ?? 0);
                    int skillId = Convert.ToInt32(row.SkillId ?? row.skillId ?? 0);
                    int subSkillId = Convert.ToInt32(row.SubSkillId ?? row.subSkillId ?? 0);

                    string fieldName = null, skillName = null, subSkillName = null;
                    if ((actionType == "ADD_OR_UPDATE_SKILL" || actionType == "UPDATE_SKILL") && fieldId > 0)
                    {
                        var fields = await _userRepository.GetAllFieldsAsync();
                        var field = ((IEnumerable<dynamic>)fields)?.FirstOrDefault(f => (int)(f.FieldId ?? f.fieldId ?? 0) == fieldId);
                        fieldName = field?.FieldName?.ToString() ?? field?.fieldName?.ToString();
                        if (skillId > 0)
                        {
                            var skills = await _userRepository.GetSkillsByFieldAsync(fieldId);
                            var sk = ((IEnumerable<dynamic>)skills)?.FirstOrDefault(s => (int)(s.SkillId ?? s.skillId ?? 0) == skillId);
                            skillName = sk?.SkillName?.ToString() ?? sk?.skillName?.ToString();
                            if (subSkillId > 0)
                            {
                                var subSkills = await _userRepository.GetSubSkillsBySkillAsync(skillId);
                                var ss = ((IEnumerable<dynamic>)subSkills)?.FirstOrDefault(s => (int)(s.SubSkillId ?? s.subSkillId ?? 0) == subSkillId);
                                subSkillName = ss?.SubSkillName?.ToString() ?? ss?.subSkillName?.ToString();
                            }
                        }
                    }

                    userChanges.Add(new
                    {
                        actionType,
                        changedAt,
                        fieldName = fieldName ?? "",
                        skillName = skillName ?? "",
                        subSkillName = subSkillName ?? ""
                    });
                }
            }

            userChanges = userChanges
                .OrderByDescending(x => ((dynamic)x).changedAt is DateTime dt ? dt : DateTime.MinValue)
                .ToList();

            return Json(new
            {
                username = user.Username ?? ("User #" + userId),
                cvPath = user.CVPath ?? "",
                portfolioUrl = user.PortfolioUrl ?? "",
                changes = userChanges
            });
        }
    }
}

