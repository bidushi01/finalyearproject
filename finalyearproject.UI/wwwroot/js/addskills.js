let allSkills = [];
let cvFile = null;

// Detect whether this page is opened by a logged-in, already-verified user
const isLoggedInMode =
    window.addSkillsConfig &&
    (window.addSkillsConfig.isLoggedInUser === true ||
        window.addSkillsConfig.isLoggedInUser === "true");

// When editing an existing skill for a logged-in user
let editingUserSkillId = null;

// Global function for back button
window.goBackToRegister = function () {
    if (isLoggedInMode) {
        window.location.href = '/User/Profile';
        return;
    }

    if (confirm("⚠️ Going back will lose your current progress. Are you sure?")) {
        window.location.href = '/Account/Register';
    }
};

$(document).ready(function () {
    console.log("=== Document Ready ===");
    console.log("jQuery version:", $.fn.jquery);
    console.log("DevExpress loaded:", typeof DevExpress !== 'undefined');

    initializeAddSkillsPage();
    loadUserSkills();
});

function initializeAddSkillsPage() {
    console.log("=== Initializing Add Skills Page ===");

    // CV Upload Handler (registration flow only)
    $("#cvUpload").on("change", function (e) {
        cvFile = e.target.files[0];
        if (cvFile) {
            const maxSize = 5 * 1024 * 1024; // 5MB
            if (maxSize && cvFile.size > maxSize) {
                $("#cvStatus").html('<span class="text-danger">❌ File too large. Maximum size is 5MB.</span>');
                cvFile = null;
                $(this).val('');
                return;
            }
            $("#cvStatus").html('<span class="text-success">✅ ' + cvFile.name + ' selected</span>');
        }
    });

    // Portfolio URL
    $("#txtPortfolio").dxTextBox({
        placeholder: "https://github.com/yourname",
        width: "100%"
    });
    console.log("✓ Portfolio URL initialized");

    // =============================
    // ✅ FIELD DROPDOWN WITH "+ ADD NEW" AT TOP
    // =============================
    $("#ddField").dxSelectBox({
        dataSource: {
            store: {
                type: "array",
                data: [],
                key: "FieldId"
            },
            sort: "FieldName"
        },
        displayExpr: 'FieldName',
        valueExpr: 'FieldId',
        placeholder: 'Select Field or Add New',
        searchEnabled: true,
        width: "100%",
        onOpened: function (e) {
            // Load fields when dropdown opens
            loadFieldsWithAddOption(e.component);
        },
        onValueChanged: function (e) {
            console.log("Field selected:", e.value);

            if (e.value === -999) {
                // User clicked "+ Add New Field"
                e.component.option("value", null);
                promptAddNewField();
            } else if (e.value) {
                loadSkillsByField(e.value);
            } else {
                $("#ddSkill").dxSelectBox("instance").option("dataSource", []);
                $("#ddSubSkill").dxTagBox("instance").option("dataSource", []);
            }
        }
    });
    console.log("✓ Field dropdown initialized");

    // =============================
    // ✅ SKILL DROPDOWN WITH "+ ADD NEW" AT TOP
    // =============================
    $("#ddSkill").dxSelectBox({
        dataSource: [],
        displayExpr: 'SkillName',
        valueExpr: 'SkillId',
        placeholder: 'Select Skill or Add New',
        searchEnabled: true,
        width: "100%",
        onValueChanged: function (e) {
            console.log("Skill selected:", e.value);

            if (e.value === -999) {
                // User clicked "+ Add New Skill"
                e.component.option("value", null);
                const fieldId = $("#ddField").dxSelectBox("instance").option("value");
                if (!fieldId) {
                    DevExpress.ui.notify("Please select a Field first", "error", 2000);
                    return;
                }
                promptAddNewSkill(fieldId);
            } else if (e.value) {
                loadSubSkillsBySkill(e.value);
            } else {
                $("#ddSubSkill").dxTagBox("instance").option("dataSource", []);
            }
        }
    });
    console.log("✓ Skill dropdown initialized");

    // =============================
    // ✅ SUB-SKILL TAGBOX WITH "+ ADD NEW" AT TOP
    // =============================
    $("#ddSubSkill").dxTagBox({
        dataSource: [],
        displayExpr: 'SubSkillName',
        valueExpr: 'SubSkillId',
        placeholder: 'Select Sub-Skill(s) or Add New',
        showSelectionControls: true,
        applyValueMode: 'useButtons',
        searchEnabled: true,
        width: "100%",
        onValueChanged: function (e) {
            // Ensure value is an array (DevExtreme expects array for TagBox)
            var values = Array.isArray(e.value) ? e.value : (e.value ? [e.value] : []);
            // Check if user selected the "+ Add New" option
            if (values.length && values.includes(-999)) {
                // Remove -999 from selection
                const newValue = values.filter(id => id !== -999);
                e.component.option("value", newValue);

                const skillId = $("#ddSkill").dxSelectBox("instance").option("value");
                if (!skillId) {
                    DevExpress.ui.notify("Please select a Skill first", "error", 2000);
                    return;
                }
                promptAddNewSubSkill(skillId);
            }
        }
    });
    console.log("✓ Sub-Skill tagbox initialized");

    // Experience Level Dropdown
    $("#ddExperience").dxSelectBox({
        dataSource: [
            { value: 1, text: 'Student / Final Year / Intern / Trainee' },
            { value: 2, text: '0-3 Years / Freelance' },
            { value: 3, text: '3-5 Years' },
            { value: 4, text: '5+ Years' }
        ],
        displayExpr: 'text',
        valueExpr: 'value',
        placeholder: 'Select Experience Level',
        width: "100%"
    });
    console.log("✓ Experience dropdown initialized");

    // Available Days TagBox
    $("#tagAvailableDays").dxTagBox({
        dataSource: ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday', 'Sunday'],
        placeholder: 'Select Available Days',
        showSelectionControls: true,
        applyValueMode: 'useButtons',
        width: "100%"
    });
    console.log("✓ Available Days tagbox initialized");

    // Time Start
    $("#timeStart").dxDateBox({
        type: 'time',
        placeholder: 'Select Start Time',
        pickerType: 'list',
        width: "100%"
    });
    console.log("✓ Time Start initialized");

    // Time End
    $("#timeEnd").dxDateBox({
        type: 'time',
        placeholder: 'Select End Time',
        pickerType: 'list',
        width: "100%"
    });
    console.log("✓ Time End initialized");

    // Add Skill Button
    $("#btnAddSkill").dxButton({
        text: "Add Skill +",
        type: "success",
        icon: "add",
        width: "auto",
        onClick: function () {
            console.log("Add Skill button clicked");
            addUserSkill();
        }
    });
    console.log("✓ Add Skill button initialized");

    // Proceed to Verification Button (registration only)
    if (!isLoggedInMode) {
        $("#btnProceedToVerification").dxButton({
            text: "Complete Registration & Verify Email →",
            type: "default",
            height: 50,
            width: "auto",
            onClick: function () {
                console.log("Proceed button clicked");
                proceedToVerification();
            }
        });
        console.log("✓ Proceed button initialized");
    } else {
        // Logged-in users do not need OTP / email verification again
        $("#cvSection").hide();
        $("#btnProceedToVerification").remove();
    }

    console.log("=== Initialization Complete ===");
}

// =============================
// ✅ LOAD FIELDS WITH "+ ADD NEW" OPTION
// =============================
function loadFieldsWithAddOption(component) {
    $.get('/Account/GetFields', function (data) {
        data = Array.isArray(data) ? data : [];
        // Normalize casing to keep DevExtreme valueExpr/displayExpr stable
        data = data.map(function (x) {
            return {
                FieldId: x.FieldId ?? x.fieldId,
                FieldName: x.FieldName ?? x.fieldName
            };
        });
        // Add "+ Add New Field" option at the top
        const fieldsWithAdd = [
            { FieldId: -999, FieldName: '+ Add New Field' }
        ].concat(data);

        component.option("dataSource", fieldsWithAdd);
    });
}

function loadSkillsByField(fieldId) {
    $.get('/Account/GetSkillsByField', { fieldId: fieldId }, function (data) {
        data = Array.isArray(data) ? data : [];
        data = data.map(function (x) {
            return {
                SkillId: x.SkillId ?? x.skillId,
                SkillName: x.SkillName ?? x.skillName
            };
        });
        // Add "+ Add New Skill" option at the top
        const skillsWithAdd = [
            { SkillId: -999, SkillName: '+ Add New Skill' }
        ].concat(data);

        const sb = $("#ddSkill").dxSelectBox("instance");
        sb.option("dataSource", skillsWithAdd);
        sb.option("value", null);
    });
}

function loadSubSkillsBySkill(skillId) {
    $.get('/Account/GetSubSkillsBySkill', { skillId: skillId }, function (data) {
        data = Array.isArray(data) ? data : [];
        data = data.map(function (x) {
            return {
                SubSkillId: x.SubSkillId ?? x.subSkillId,
                SubSkillName: x.SubSkillName ?? x.subSkillName
            };
        });
        // Add "+ Add New Sub-Skill" option at the top
        const subSkillsWithAdd = [
            { SubSkillId: -999, SubSkillName: '+ Add New Sub-Skill' }
        ].concat(data);

        const tb = $("#ddSubSkill").dxTagBox("instance");
        tb.option("dataSource", subSkillsWithAdd);
        tb.option("value", []);
    });
}

// =============================
// ✅ ADD NEW FIELD WITH POPUP
// =============================
function promptAddNewField() {
    DevExpress.ui.dialog.custom({
        title: "Add New Field",
        messageHtml: '<div><label>Field Name:</label><input id="newFieldInput" type="text" class="form-control" placeholder="e.g., Data Science" /></div>',
        buttons: [
            {
                text: "Add",
                onClick: function () {
                    const fieldName = $("#newFieldInput").val().trim();
                    if (!fieldName) {
                        DevExpress.ui.notify("Please enter a field name", "warning", 2000);
                        return false;
                    }

                    addNewFieldToServer(fieldName);
                    return true;
                }
            },
            {
                text: "Cancel"
            }
        ]
    }).show();

    // Focus on input after dialog appears
    setTimeout(() => $("#newFieldInput").focus(), 100);
}

function addNewFieldToServer(fieldName) {
    $.post("/Account/AddNewField", { fieldName: fieldName })
        .done(function (data) {
            if (data.success) {
                DevExpress.ui.notify(data.message, "success", 2000);
                // Reload fields and select the new one
                const fieldBox = $("#ddField").dxSelectBox("instance");
                loadFieldsWithAddOption(fieldBox);
                setTimeout(() => fieldBox.option("value", data.fieldId), 300);
            } else {
                DevExpress.ui.notify(data.message || "Field already exists", "warning", 2500);
                // If field exists, try to select it
                if (data.fieldId && data.fieldId > 0) {
                    const fieldBox = $("#ddField").dxSelectBox("instance");
                    loadFieldsWithAddOption(fieldBox);
                    setTimeout(() => fieldBox.option("value", data.fieldId), 300);
                }
            }
        })
        .fail(function () {
            DevExpress.ui.notify("Error adding field", "error", 2000);
        });
}

// =============================
// ✅ ADD NEW SKILL WITH POPUP
// =============================
function promptAddNewSkill(fieldId) {
    DevExpress.ui.dialog.custom({
        title: "Add New Skill",
        messageHtml: '<div><label>Skill Name:</label><input id="newSkillInput" type="text" class="form-control" placeholder="e.g., Machine Learning" /></div>',
        buttons: [
            {
                text: "Add",
                onClick: function () {
                    const skillName = $("#newSkillInput").val().trim();
                    if (!skillName) {
                        DevExpress.ui.notify("Please enter a skill name", "warning", 2000);
                        return false;
                    }

                    addNewSkillToServer(fieldId, skillName);
                    return true;
                }
            },
            {
                text: "Cancel"
            }
        ]
    }).show();

    setTimeout(() => $("#newSkillInput").focus(), 100);
}

function addNewSkillToServer(fieldId, skillName) {
    $.post("/Account/AddNewSkill", { fieldId: fieldId, skillName: skillName })
        .done(function (data) {
            if (data.success) {
                DevExpress.ui.notify(data.message, "success", 2000);
                loadSkillsByField(fieldId);
                setTimeout(() => $("#ddSkill").dxSelectBox("instance").option("value", data.skillId), 300);
            } else {
                DevExpress.ui.notify(data.message || "Skill already exists", "warning", 2500);
                if (data.skillId && data.skillId > 0) {
                    loadSkillsByField(fieldId);
                    setTimeout(() => $("#ddSkill").dxSelectBox("instance").option("value", data.skillId), 300);
                }
            }
        })
        .fail(function () {
            DevExpress.ui.notify("Error adding skill", "error", 2000);
        });
}

// =============================
// ✅ ADD NEW SUB-SKILL WITH POPUP
// =============================
function promptAddNewSubSkill(skillId) {
    DevExpress.ui.dialog.custom({
        title: "Add New Sub-Skill",
        messageHtml: '<div><label>Sub-Skill Name:</label><input id="newSubSkillInput" type="text" class="form-control" placeholder="e.g., TensorFlow" /></div>',
        buttons: [
            {
                text: "Add",
                onClick: function () {
                    const subSkillName = $("#newSubSkillInput").val().trim();
                    if (!subSkillName) {
                        DevExpress.ui.notify("Please enter a sub-skill name", "warning", 2000);
                        return false;
                    }

                    addNewSubSkillToServer(skillId, subSkillName);
                    return true;
                }
            },
            {
                text: "Cancel"
            }
        ]
    }).show();

    setTimeout(() => $("#newSubSkillInput").focus(), 100);
}

function addNewSubSkillToServer(skillId, subSkillName) {
    $.post("/Account/AddNewSubSkill", { skillId: skillId, subSkillName: subSkillName })
        .done(function (data) {
            var newOrExistingId = data ? (data.subSkillId ?? data.SubSkillId) : null;
            if (data.success) {
                DevExpress.ui.notify(data.message, "success", 2000);
                loadSubSkillsBySkill(skillId);
                setTimeout(function () {
                    const subSkillTagBox = $("#ddSubSkill").dxTagBox("instance");
                    const currentValues = subSkillTagBox.option("value") || [];
                    if (newOrExistingId) currentValues.push(newOrExistingId);
                    subSkillTagBox.option("value", currentValues);
                }, 300);
            } else {
                DevExpress.ui.notify(data.message || "Sub-skill already exists", "warning", 2500);
                if (newOrExistingId && newOrExistingId > 0) {
                    loadSubSkillsBySkill(skillId);
                    setTimeout(function () {
                        const subSkillTagBox = $("#ddSubSkill").dxTagBox("instance");
                        const currentValues = subSkillTagBox.option("value") || [];
                        currentValues.push(newOrExistingId);
                        subSkillTagBox.option("value", currentValues);
                    }, 300);
                }
            }
        })
        .fail(function () {
            DevExpress.ui.notify("Error adding sub-skill", "error", 2000);
        });
}

function addUserSkill() {
    const fieldId = $("#ddField").dxSelectBox("instance").option("value");
    const skillId = $("#ddSkill").dxSelectBox("instance").option("value");
    const subSkillIds = $("#ddSubSkill").dxTagBox("instance").option("value");
    const experienceLevel = $("#ddExperience").dxSelectBox("instance").option("value");
    const availableDays = $("#tagAvailableDays").dxTagBox("instance").option("value");
    const timeStart = $("#timeStart").dxDateBox("instance").option("value");
    const timeEnd = $("#timeEnd").dxDateBox("instance").option("value");

    // Front-end validation
    if (!fieldId || fieldId === -999 || !skillId || skillId === -999 || !subSkillIds || subSkillIds.length === 0 || !experienceLevel || !availableDays || availableDays.length === 0 || !timeStart || !timeEnd) {
        DevExpress.ui.notify("Please fill in all fields", "warning", 2000);
        return;
    }

    // Prevent adding duplicate combinations (ignore current record when editing)
    const hasDuplicate = subSkillIds.some(function (subSkillId) {
        return (allSkills || []).some(function (s) {
            const sameCombo =
                parseInt(s.FieldId) === parseInt(fieldId) &&
                parseInt(s.SkillId) === parseInt(skillId) &&
                parseInt(s.SubSkillId) === parseInt(subSkillId);

            if (!sameCombo) return false;

            if (!isLoggedInMode || !editingUserSkillId) return true;

            const existingId = s.UserSkillId || s.userSkillId || null;
            return existingId !== editingUserSkillId;
        });
    });

    if (hasDuplicate) {
        DevExpress.ui.notify("This skill is already in your list.", "warning", 2500);
        return;
    }

    const formatTime = function (date) {
        if (!date) return '';
        const hours = date.getHours().toString().padStart(2, '0');
        const minutes = date.getMinutes().toString().padStart(2, '0');
        const seconds = date.getSeconds().toString().padStart(2, '0');
        return `${hours}:${minutes}:${seconds}`;
    };

    // If logged in and editing a single existing record, call update endpoint
    if (isLoggedInMode && editingUserSkillId) {
        const model = {
            UserSkillId: editingUserSkillId,
            FieldId: fieldId,
            SkillId: skillId,
            SubSkillId: subSkillIds[0],
            ExperienceLevel: experienceLevel,
            AvailableDays: availableDays.join(','),
            AvailableTimeStart: formatTime(timeStart),
            AvailableTimeEnd: formatTime(timeEnd)
        };

        $.ajax({
            url: '/Account/UpdateUserSkillForLoggedIn',
            type: 'POST',
            contentType: 'application/json',
            data: JSON.stringify(model)
        }).done(function (data) {
            if (data && data.success) {
                DevExpress.ui.notify("Skill updated successfully", "success", 2000);
                loadUserSkills();
                clearForm();
            } else {
                DevExpress.ui.notify(data && data.message ? data.message : "Error updating skill", "error", 2000);
            }
        }).fail(function () {
            DevExpress.ui.notify("Error updating skill", "error", 2000);
        });

        return;
    }

    // Otherwise, add one or more new skills
    let requests = [];

    subSkillIds.forEach(function (subSkillId) {
        const model = {
            FieldId: fieldId,
            SkillId: skillId,
            SubSkillId: subSkillId,
            ExperienceLevel: experienceLevel,
            AvailableDays: availableDays.join(','),
            AvailableTimeStart: formatTime(timeStart),
            AvailableTimeEnd: formatTime(timeEnd)
        };

        const url = isLoggedInMode
            ? '/Account/AddUserSkillForLoggedIn'
            : '/Account/AddUserSkill';

        const request = $.ajax({
            url: url,
            type: 'POST',
            contentType: 'application/json',
            data: JSON.stringify(model)
        });

        requests.push(request);
    });

    $.when.apply($, requests).done(function () {
        DevExpress.ui.notify("Skill(s) added successfully", "success", 2000);
        loadUserSkills();
        clearForm();
    }).fail(function () {
        DevExpress.ui.notify("Error adding skill(s)", "error", 2000);
    });
}

function loadUserSkills() {
    $.ajax({
        url: isLoggedInMode ? '/Account/GetCurrentUserSkills' : '/Account/GetUserSkills',
        type: 'GET',
        success: function (skills) {
            allSkills = skills || [];
            displayUserSkills();
        },
        error: function () {
            console.error("Error loading skills");
            allSkills = [];
            displayUserSkills();
        }
    });
}

function displayUserSkills() {
    const container = $("#skillsContainer");
    container.empty();

    if (allSkills.length === 0) {
        const message = isLoggedInMode
            ? 'No skills found. Please add at least one skill to use the platform.'
            : 'No skills added yet. Please add at least one skill to proceed.';

        container.html('<div class="alert alert-warning">' + message + '</div>');
        return;
    }

    allSkills.forEach(function (skill, index) {
        const expLevels = [
            '',
            'Student / Final Year / Intern / Trainee',
            '0-3 Years / Freelance',
            '3-5 Years',
            '5+ Years'
        ];

        const canDelete = !isLoggedInMode && allSkills.length > 1;
        const showEdit = isLoggedInMode;

        const editButton = showEdit
            ? `<button class="btn btn-outline-light btn-sm me-2" onclick="editExistingSkill(${index})">Edit</button>`
            : '';

        const deleteButton = canDelete
            ? `<button class="btn btn-danger btn-sm" onclick="deleteSkill('${skill.TempId}')">Delete</button>`
            : '';

        const card = `
            <div class="card mb-2">
                <div class="card-body">
                    <div class="row">
                        <div class="col-md-10">
                            <h6 class="mb-1"><strong>${skill.FieldName}</strong> > ${skill.SkillName} > ${skill.SubSkillName}</h6>
                            <p class="mb-1"><span class="badge bg-info">${expLevels[skill.ExperienceLevel]}</span></p>
                            <small class="text-muted">
                                📅 ${skill.AvailableDays} |
                                🕐 ${formatTime(skill.AvailableTimeStart)} - ${formatTime(skill.AvailableTimeEnd)}
                            </small>
                        </div>
                        <div class="col-md-2 text-end">
                            ${editButton}
                            ${deleteButton}
                        </div>
                    </div>
                </div>
            </div>
        `;
        container.append(card);
    });
}

function formatTime(timeStr) {
    if (!timeStr) return '';
    return timeStr.substring(0, 5);
}

function deleteSkill(tempId) {
    if (!confirm("Are you sure you want to delete this skill?")) return;

    $.post('/Account/DeleteUserSkill', { tempId: tempId }, function (data) {
        if (data.success) {
            DevExpress.ui.notify(data.message, "success", 2000);
            loadUserSkills();
        } else {
            DevExpress.ui.notify(data.message, "error", 2000);
        }
    });
}

function clearForm() {
    editingUserSkillId = null;

    $("#ddField").dxSelectBox("instance").option("value", null);
    $("#ddSkill").dxSelectBox("instance").option("dataSource", []);
    $("#ddSkill").dxSelectBox("instance").option("value", null);
    $("#ddSubSkill").dxTagBox("instance").option("dataSource", []);
    $("#ddSubSkill").dxTagBox("instance").option("value", []);
    $("#ddExperience").dxSelectBox("instance").option("value", null);
    $("#tagAvailableDays").dxTagBox("instance").option("value", []);
    $("#timeStart").dxDateBox("instance").option("value", null);
    $("#timeEnd").dxDateBox("instance").option("value", null);
}

// Load an existing DB skill into the form so a logged-in user can edit it.
window.editExistingSkill = function (index) {
    if (!isLoggedInMode) return;

    const skill = allSkills[index];
    if (!skill) return;

    const fieldId = skill.FieldId || skill.fieldId;
    const skillId = skill.SkillId || skill.skillId;
    const subSkillId = skill.SubSkillId || skill.subSkillId;
    const expLevel = skill.ExperienceLevel || skill.experienceLevel;
    const daysStr = skill.AvailableDays || skill.availableDays || '';
    const days = daysStr ? daysStr.split(',') : [];

    editingUserSkillId = skill.UserSkillId || skill.userSkillId || null;

    const fieldBox = $("#ddField").dxSelectBox("instance");
    const skillBox = $("#ddSkill").dxSelectBox("instance");
    const subBox = $("#ddSubSkill").dxTagBox("instance");
    const expBox = $("#ddExperience").dxSelectBox("instance");
    const daysBox = $("#tagAvailableDays").dxTagBox("instance");
    const startBox = $("#timeStart").dxDateBox("instance");
    const endBox = $("#timeEnd").dxDateBox("instance");

    if (fieldId) {
        loadFieldsWithAddOption(fieldBox);
        setTimeout(function () {
            fieldBox.option("value", fieldId);

            if (skillId) {
                loadSkillsByField(fieldId);
                setTimeout(function () {
                    skillBox.option("value", skillId);

                    if (subSkillId) {
                        loadSubSkillsBySkill(skillId);
                        setTimeout(function () {
                            subBox.option("value", [subSkillId]);
                        }, 300);
                    }
                }, 300);
            }
        }, 300);
    }

    expBox.option("value", expLevel || null);
    daysBox.option("value", days.filter(function (d) { return d; }));

    const toDate = function (timeStr) {
        if (!timeStr) return null;
        const parts = timeStr.toString().split(':');
        if (parts.length < 2) return null;
        const d = new Date();
        d.setHours(parseInt(parts[0], 10), parseInt(parts[1], 10), 0, 0);
        return d;
    };

    startBox.option("value", toDate(skill.AvailableTimeStart || skill.availableTimeStart));
    endBox.option("value", toDate(skill.AvailableTimeEnd || skill.availableTimeEnd));

    DevExpress.ui.notify("Loaded skill into the form. Adjust values and click 'Add Skill +' to save changes.", "info", 3000);
};

function proceedToVerification() {
    if (allSkills.length === 0) {
        DevExpress.ui.notify("Please add at least one skill before proceeding", "warning", 3000);
        return;
    }

    if (!cvFile) {
        DevExpress.ui.notify("Please upload your CV before proceeding", "warning", 3000);
        return;
    }

    const formData = new FormData();
    formData.append('cv', cvFile);
    formData.append('portfolioUrl', $("#txtPortfolio").dxTextBox("instance").option("value") || '');

    $.ajax({
        url: '/Account/UploadCVAndProceed',
        type: 'POST',
        data: formData,
        processData: false,
        contentType: false,
        success: function (data) {
            if (data.success) {
                window.location.href = data.redirectUrl;
            } else {
                DevExpress.ui.notify(data.message, "error", 2000);
            }
        },
        error: function () {
            DevExpress.ui.notify("Error uploading CV", "error", 2000);
        }
    });
}