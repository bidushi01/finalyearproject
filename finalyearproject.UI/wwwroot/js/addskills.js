let allSkills = [];
let cvFile = null;


const isLoggedInMode =
    window.addSkillsConfig &&
    (window.addSkillsConfig.isLoggedInUser === true ||
        window.addSkillsConfig.isLoggedInUser === "true");

// When editing an existing skill for a logged-in user
let editingUserSkillId = null;
let editingTempId = null;
let timeSlotRows = [];

function timeToDate(timeStr) {
    if (!timeStr) return null;
    var parts = timeStr.toString().split(':');
    if (parts.length < 2) return null;
    var d = new Date();
    d.setHours(parseInt(parts[0], 10), parseInt(parts[1], 10), 0, 0);
    return d;
}

function formatTimeValue(date) {
    if (!date) return '';
    var hours = date.getHours().toString().padStart(2, '0');
    var minutes = date.getMinutes().toString().padStart(2, '0');
    var seconds = date.getSeconds().toString().padStart(2, '0');
    return hours + ':' + minutes + ':' + seconds;
}

function refreshRemoveSlotButtons() {
    var showRemove = timeSlotRows.length > 1;
    $('.time-slot-row .btn-remove-slot').toggle(showRemove);
}

function addTimeSlotRow(startVal, endVal) {
    var rowId = 'slot_' + Date.now() + '_' + Math.random().toString(36).slice(2, 6);
    var rowHtml =
        '<div class="row mb-2 align-items-end time-slot-row" data-row-id="' + rowId + '">' +
        '<div class="col-md-5"><label class="form-label small">From</label><div id="' + rowId + '_start"></div></div>' +
        '<div class="col-md-5"><label class="form-label small">To (same day)</label><div id="' + rowId + '_end"></div></div>' +
        '<div class="col-md-2"><button type="button" class="btn btn-outline-danger btn-sm btn-remove-slot">Remove</button></div>' +
        '</div>';
    $('#timeSlotsList').append(rowHtml);

    var startInst = $('#' + rowId + '_start').dxDateBox({
        type: 'time', pickerType: 'list', width: '100%', value: startVal || null
    }).dxDateBox('instance');
    var endInst = $('#' + rowId + '_end').dxDateBox({
        type: 'time', pickerType: 'list', width: '100%', value: endVal || null
    }).dxDateBox('instance');

    timeSlotRows.push({ id: rowId, startBox: startInst, endBox: endInst });

    $('[data-row-id="' + rowId + '"] .btn-remove-slot').on('click', function () {
        removeTimeSlotRow(rowId);
    });
    refreshRemoveSlotButtons();
}

function removeTimeSlotRow(rowId) {
    if (timeSlotRows.length <= 1) return;
    var row = timeSlotRows.find(function (r) { return r.id === rowId; });
    if (row) {
        try { row.startBox.dispose(); row.endBox.dispose(); } catch (e) { }
    }
    timeSlotRows = timeSlotRows.filter(function (r) { return r.id !== rowId; });
    $('[data-row-id="' + rowId + '"]').remove();
    refreshRemoveSlotButtons();
}

function resetTimeSlots() {
    timeSlotRows.forEach(function (r) {
        try { r.startBox.dispose(); r.endBox.dispose(); } catch (e) { }
    });
    timeSlotRows = [];
    $('#timeSlotsList').empty();
    addTimeSlotRow(null, null);
}

function initTimeSlots() {
    resetTimeSlots();
    $('#btnAddTimeSlot').off('click').on('click', function () {
        addTimeSlotRow(null, null);
    });
}

function collectTimeSlots() {
    return timeSlotRows.map(function (r) {
        return { start: r.startBox.option('value'), end: r.endBox.option('value') };
    }).filter(function (s) { return s.start && s.end; });
}

function slotMinutes(slot) {
    return {
        start: slot.start.getHours() * 60 + slot.start.getMinutes(),
        end: slot.end.getHours() * 60 + slot.end.getMinutes()
    };
}

function validateTimeSlots(slots) {
    if (!slots.length) return 'Please add at least one availability time slot.';
    var ranges = [];
    for (var i = 0; i < slots.length; i++) {
        var r = slotMinutes(slots[i]);
        if (r.end <= r.start) {
            return 'Slot ' + (i + 1) + ': end time must be later on the same day (e.g. 3:00 PM to 11:00 PM, not 3:00 PM to 2:00 PM).';
        }
        ranges.push(r);
    }
    for (var a = 0; a < ranges.length; a++) {
        for (var b = a + 1; b < ranges.length; b++) {
            if (ranges[a].start < ranges[b].end && ranges[b].start < ranges[a].end) {
                return 'Time slots on the same day cannot overlap. Use separate slots such as 2:00 PM-5:00 PM and 9:00 PM-11:00 PM.';
            }
        }
    }
    return null;
}

function buildTimePayload(slots) {
    var sorted = slots.slice().sort(function (a, b) {
        return slotMinutes(a).start - slotMinutes(b).start;
    });
    var formatted = sorted.map(function (s) {
        return { start: formatTimeValue(s.start), end: formatTimeValue(s.end) };
    });
    return {
        AvailableTimeStart: formatted[0].start,
        AvailableTimeEnd: formatted[formatted.length - 1].end,
        AvailableTimeSlots: JSON.stringify(formatted)
    };
}

function parseTimeSlotsFromSkill(skill) {
    var json = skill.AvailableTimeSlots || skill.availableTimeSlots;
    if (json) {
        try {
            var arr = typeof json === 'string' ? JSON.parse(json) : json;
            if (Array.isArray(arr) && arr.length) return arr;
        } catch (e) { }
    }
    var s = skill.AvailableTimeStart || skill.availableTimeStart;
    var e = skill.AvailableTimeEnd || skill.availableTimeEnd;
    if (s && e) return [{ start: s, end: e }];
    return [];
}

function formatAvailabilityText(skill) {
    var slots = parseTimeSlotsFromSkill(skill);
    if (slots.length > 1) {
        return slots.map(function (s) {
            return formatTime(s.start) + ' - ' + formatTime(s.end);
        }).join(', ');
    }
    if (slots.length === 1) {
        return formatTime(slots[0].start) + ' - ' + formatTime(slots[0].end);
    }
    return formatTime(skill.AvailableTimeStart || skill.availableTimeStart) + ' - ' +
        formatTime(skill.AvailableTimeEnd || skill.availableTimeEnd);
}

function loadTimeSlotsFromSkill(skill) {
    resetTimeSlots();
    var slots = parseTimeSlotsFromSkill(skill);
    if (!slots.length) return;
    timeSlotRows.forEach(function (r) {
        try { r.startBox.dispose(); r.endBox.dispose(); } catch (e) { }
    });
    timeSlotRows = [];
    $('#timeSlotsList').empty();
    slots.forEach(function (s) {
        addTimeSlotRow(timeToDate(s.start), timeToDate(s.end));
    });
}

// Global function for back button
window.goBackToRegister = function () {
    if (isLoggedInMode) {
        window.location.href = '/User/Profile';
        return;
    }

    if (confirm(" Going back will lose your current progress. Are you sure?")) {
        window.location.href = '/Account/Register';
    }
};

$(document).ready(function () {
    initializeAddSkillsPage();
    loadUserSkills();
});

function initializeAddSkillsPage() {

    // CV Upload Handler (registration flow only)
    $("#cvUpload").on("change", function (e) {
        cvFile = e.target.files[0];
        if (cvFile) {
            const maxSize = 5 * 1024 * 1024; // 5MB
            if (maxSize && cvFile.size > maxSize) {
                $("#cvStatus").html('<span class="text-danger"> File too large. Maximum size is 5MB.</span>');
                cvFile = null;
                $(this).val('');
                return;
            }
            $("#cvStatus").html(
                '<span class="text-success">' + cvFile.name + ' selected</span>' +
                '<div class="small text-warning mt-1">Click <strong>Save CV &amp; Portfolio</strong>, or submit skills below (CV uploads automatically).</div>'
            );
        }
    });

    // Portfolio URL
    $("#txtPortfolio").dxTextBox({
        placeholder: "https://github.com/yourname",
        width: "100%"
    });
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

    $("#ddSkill").dxSelectBox({
        dataSource: [],
        displayExpr: 'SkillName',
        valueExpr: 'SkillId',
        placeholder: 'Select Skill or Add New',
        searchEnabled: true,
        width: "100%",
        onValueChanged: function (e) {
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

    $("#tagAvailableDays").dxTagBox({
        dataSource: ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday', 'Sunday'],
        placeholder: 'Select Available Days',
        showSelectionControls: true,
        applyValueMode: 'useButtons',
        width: "100%"
    });

    initTimeSlots();

    $("#btnAddSkill").dxButton({
        text: "Add Skill +",
        type: "success",
        icon: "add",
        width: "auto",
        onClick: function () {
            addUserSkill();
        }
    });

    if (isLoggedInMode) {
        $("#btnSaveCvPortfolio").on("click", saveCvPortfolioLoggedIn);
        loadLoggedInCvPortfolio();
        $("#btnSubmitForAdminReview").on("click", submitSkillsForAdminReview);
    }
}

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

// ADD NEW SKILL WITH POPUP

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
    const timeSlots = collectTimeSlots();
    const slotError = validateTimeSlots(timeSlots);

    if (!fieldId || fieldId === -999 || !skillId || skillId === -999 || !subSkillIds || subSkillIds.length === 0 || !experienceLevel || !availableDays || availableDays.length === 0 || slotError) {
        DevExpress.ui.notify(slotError || "Please fill in all fields", "warning", 2500);
        return;
    }

    const timePayload = buildTimePayload(timeSlots);

    function skillIdsMatch(s, fId, skId, subId) {
        return parseInt(s.FieldId || s.fieldId, 10) === parseInt(fId, 10) &&
            parseInt(s.SkillId || s.skillId, 10) === parseInt(skId, 10) &&
            parseInt(s.SubSkillId || s.subSkillId, 10) === parseInt(subId, 10);
    }

    // Prevent adding duplicate combinations (ignore current record when editing)
    const hasDuplicate = subSkillIds.some(function (subSkillId) {
        return (allSkills || []).some(function (s) {
            if (!skillIdsMatch(s, fieldId, skillId, subSkillId)) return false;

            if (!isLoggedInMode) return true;

            const tempId = s.TempId || s.tempId;
            if (editingTempId && tempId === editingTempId) return false;

            const existingId = s.UserSkillId || s.userSkillId || null;
            if (editingUserSkillId && existingId === editingUserSkillId) return false;

            return true;
        });
    });

    if (hasDuplicate) {
        DevExpress.ui.notify("This skill is already in your list.", "warning", 2500);
        return;
    }

    const formatTime = formatTimeValue;

    // Draft edit: update session only
    if (isLoggedInMode && editingTempId) {
        const firstSubSkill = subSkillIds[0];
        const draftModel = {
            TempId: editingTempId,
            FieldId: fieldId,
            SkillId: skillId,
            SubSkillId: firstSubSkill,
            ExperienceLevel: experienceLevel,
            AvailableDays: availableDays.join(','),
            AvailableTimeStart: timePayload.AvailableTimeStart,
            AvailableTimeEnd: timePayload.AvailableTimeEnd,
            AvailableTimeSlots: timePayload.AvailableTimeSlots
        };

        $.ajax({
            url: '/Account/AddUserSkillForLoggedIn',
            type: 'POST',
            contentType: 'application/json',
            data: JSON.stringify(draftModel)
        }).done(function (d) {
            if (d && d.success) {
                DevExpress.ui.notify(d.message || 'Draft updated', 'success', 2200);
                loadUserSkills();
                clearForm();
            } else {
                DevExpress.ui.notify((d && d.message) || 'Could not update', 'error', 2500);
            }
        });
        return;
    }

    // If logged in and editing a single existing DB record, call update endpoint.
    if (isLoggedInMode && editingUserSkillId) {
        const firstSubSkill = subSkillIds[0];
        const updateModel = {
            UserSkillId: editingUserSkillId,
            FieldId: fieldId,
            SkillId: skillId,
            SubSkillId: firstSubSkill,
            ExperienceLevel: experienceLevel,
            AvailableDays: availableDays.join(','),
            AvailableTimeStart: timePayload.AvailableTimeStart,
            AvailableTimeEnd: timePayload.AvailableTimeEnd,
            AvailableTimeSlots: timePayload.AvailableTimeSlots
        };

        const updateReq = $.ajax({
            url: '/Account/UpdateUserSkillForLoggedIn',
            type: 'POST',
            contentType: 'application/json',
            data: JSON.stringify(updateModel)
        });

        const extraSubSkills = subSkillIds.slice(1);
        const addReqs = extraSubSkills.map(function (sid) {
            const addModel = {
                FieldId: fieldId,
                SkillId: skillId,
                SubSkillId: sid,
                ExperienceLevel: experienceLevel,
                AvailableDays: availableDays.join(','),
                AvailableTimeStart: timePayload.AvailableTimeStart,
                AvailableTimeEnd: timePayload.AvailableTimeEnd,
                AvailableTimeSlots: timePayload.AvailableTimeSlots
            };
            return $.ajax({
                url: '/Account/AddUserSkillForLoggedIn',
                type: 'POST',
                contentType: 'application/json',
                data: JSON.stringify(addModel)
            });
        });

        $.when.apply($, [updateReq].concat(addReqs)).done(function () {
            DevExpress.ui.notify("Skill(s) saved successfully", "success", 2200);
            loadUserSkills();
            clearForm();
        }).fail(function () {
            DevExpress.ui.notify("Error saving skill(s)", "error", 2200);
        });

        return;
    }

    // Otherwise, add one or more new skills
    const url = isLoggedInMode ? '/Account/AddUserSkillForLoggedIn' : '/Account/AddUserSkill';

    function postOneSkill(subSkillId) {
        const model = {
            FieldId: fieldId,
            SkillId: skillId,
            SubSkillId: subSkillId,
            ExperienceLevel: experienceLevel,
            AvailableDays: availableDays.join(','),
            AvailableTimeStart: timePayload.AvailableTimeStart,
            AvailableTimeEnd: timePayload.AvailableTimeEnd,
            AvailableTimeSlots: timePayload.AvailableTimeSlots
        };
        return $.ajax({
            url: url,
            type: 'POST',
            contentType: 'application/json',
            data: JSON.stringify(model)
        }).then(function (d) {
            if (!d || d.success === false) {
                return $.Deferred().reject(d || { message: 'Could not add skill' });
            }
            return d;
        });
    }

    const chain = subSkillIds.reduce(function (p, subSkillId) {
        return p.then(function () { return postOneSkill(subSkillId); });
    }, $.Deferred().resolve().promise());

    chain.done(function () {
        var msg = isLoggedInMode
            ? "Skill(s) added to your list. Save CV and click Submit for admin verification."
            : "Skill(s) added successfully";
        DevExpress.ui.notify(msg, "success", 2800);
        loadUserSkills();
        clearForm();
    }).fail(function (xhr) {
        var err = (xhr && xhr.message) ? xhr.message
            : (xhr && xhr.responseJSON && xhr.responseJSON.message) ? xhr.responseJSON.message
            : "Error adding skill(s)";
        DevExpress.ui.notify(err, "error", 4000);
    });
}

function updateDraftCount(count) {
    var el = $('#draftSkillCount');
    if (el.length) el.text(count);
    var btn = $('#btnSubmitForAdminReview');
    if (btn.length) btn.prop('disabled', count === 0);
}

function submitSkillsForAdminReview() {
    if (!confirm('Send your draft skills to admin for verification?')) return;

    function postSubmit(fd) {
        $.ajax({
            url: '/Account/SubmitSkillsForAdminReview',
            type: 'POST',
            data: fd || null,
            processData: !fd,
            contentType: fd ? false : 'application/x-www-form-urlencoded'
        }).done(function (d) {
            if (d && d.success) {
                DevExpress.ui.notify(d.message, 'success', 4000);
                cvFile = null;
                $('#cvUpload').val('');
                loadUserSkills();
                loadLoggedInCvPortfolio();
            } else {
                DevExpress.ui.notify((d && d.message) || 'Submit failed', 'error', 5000);
            }
        }).fail(function (xhr) {
            var msg = (xhr.responseJSON && xhr.responseJSON.message) ? xhr.responseJSON.message : 'Submit failed';
            DevExpress.ui.notify(msg, 'error', 5000);
        });
    }

    if (cvFile) {
        var fd = new FormData();
        fd.append('cv', cvFile);
        postSubmit(fd);
        return;
    }

    postSubmit(null);
}

window.deleteDraftSkill = function (tempId) {
    if (!confirm('Remove this draft skill?')) return;
    $.post('/Account/DeleteDraftSkill', { tempId: tempId })
        .done(function (d) {
            if (d && d.success) {
                DevExpress.ui.notify(d.message || 'Removed', 'success', 2000);
                loadUserSkills();
            } else {
                DevExpress.ui.notify((d && d.message) || 'Could not remove', 'error', 2500);
            }
        });
};

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
        container.html('<div class="alert alert-warning">No skills on your profile yet. Add skills below, save your CV, then submit for admin verification.</div>');
        updateDraftCount(0);
        return;
    }

    var draftCount = allSkills.filter(function (s) {
        var st = (s.ApprovalStatus || s.approvalStatus || '').toString().toLowerCase();
        return st === 'draft';
    }).length;
    updateDraftCount(draftCount);

    allSkills.forEach(function (skill, index) {
        const expLevels = [
            '',
            'Student / Final Year / Intern / Trainee',
            '0-3 Years / Freelance',
            '3-5 Years',
            '5+ Years'
        ];

        const status = (skill.ApprovalStatus || skill.approvalStatus || 'Pending').toString();
        const statusLower = status.toLowerCase();
        let badgeClass = 'badge-pending';
        if (statusLower === 'approved') badgeClass = 'badge-approved';
        if (statusLower === 'rejected') badgeClass = 'badge-rejected';
        if (statusLower === 'draft') badgeClass = 'badge-draft';

        const userSkillId = skill.UserSkillId || skill.userSkillId;
        const tempId = skill.TempId || skill.tempId;
        const canEdit = statusLower === 'draft' || statusLower === 'approved' || statusLower === 'rejected';
        const editButton = canEdit
            ? `<button class="btn btn-outline-light btn-sm me-2" onclick="editExistingSkill(${index})">Edit</button>`
            : '';
        let deleteButton = '';
        if (statusLower === 'draft' && tempId) {
            deleteButton = `<button class="btn btn-danger btn-sm" onclick="deleteDraftSkill('${tempId}')">Delete</button>`;
        } else if (userSkillId && statusLower !== 'pending') {
            deleteButton = `<button class="btn btn-danger btn-sm" onclick="deleteLoggedInSkill(${userSkillId})">Delete</button>`;
        } else if (userSkillId && statusLower === 'pending') {
            deleteButton = `<button class="btn btn-danger btn-sm" onclick="deleteLoggedInSkill(${userSkillId})">Delete</button>`;
        }

        const rejectNote = (skill.SkillRejectionReason || skill.skillRejectionReason)
            ? `<div class="small text-danger mt-1">${skill.SkillRejectionReason || skill.skillRejectionReason}</div>` : '';

        const card = `
            <div class="card mb-2">
                <div class="card-body">
                    <div class="row">
                        <div class="col-md-10">
                            <h6 class="mb-1"><strong>${skill.FieldName || skill.fieldName}</strong> &gt; ${skill.SkillName || skill.skillName} &gt; ${skill.SubSkillName || skill.subSkillName}</h6>
                            <p class="mb-1">
                                <span class="badge bg-info">${expLevels[skill.ExperienceLevel || skill.experienceLevel] || ''}</span>
                                <span class="badge ${badgeClass} ms-1">${status}</span>
                            </p>
                            <small class="text-muted">
                                 ${skill.AvailableDays || skill.availableDays} |
                                 ${formatAvailabilityText(skill)}
                            </small>
                            ${rejectNote}
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

window.deleteLoggedInSkill = function (userSkillId) {
    if (!confirm('Remove this skill from your portfolio?')) return;
    $.post('/Account/DeleteUserSkillForLoggedIn', { userSkillId: userSkillId })
        .done(function (d) {
            if (d && d.success) {
                DevExpress.ui.notify(d.message || 'Deleted', 'success', 2000);
                loadUserSkills();
            } else {
                DevExpress.ui.notify((d && d.message) || 'Could not delete', 'error', 2500);
            }
        });
};

function loadLoggedInCvPortfolio() {
    $.getJSON('/User/GetUserInfo').done(function (info) {
        if (info && info.portfolioUrl && $("#txtPortfolio").dxTextBox) {
            $("#txtPortfolio").dxTextBox('instance').option('value', info.portfolioUrl);
        }
        if (info && info.cvPath) {
            $("#cvStatus").html('<span class="text-muted">Current CV on file. Upload a new file to replace it.</span>');
        }
    });
}

function saveCvPortfolioLoggedIn() {
    var fd = new FormData();
    if (cvFile) fd.append('cv', cvFile);
    var portfolio = $("#txtPortfolio").dxTextBox('instance').option('value') || '';
    fd.append('portfolioUrl', portfolio);

    $.ajax({
        url: '/Account/UpdateDocumentsForLoggedIn',
        type: 'POST',
        data: fd,
        processData: false,
        contentType: false
    }).done(function (d) {
        if (d && d.success) {
            DevExpress.ui.notify(d.message || 'Saved', 'success', 2500);
            cvFile = null;
            $("#cvUpload").val('');
        } else {
            DevExpress.ui.notify((d && d.message) || 'Save failed', 'error', 2500);
        }
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
    editingTempId = null;

    $("#ddField").dxSelectBox("instance").option("value", null);
    $("#ddSkill").dxSelectBox("instance").option("dataSource", []);
    $("#ddSkill").dxSelectBox("instance").option("value", null);
    $("#ddSubSkill").dxTagBox("instance").option("dataSource", []);
    $("#ddSubSkill").dxTagBox("instance").option("value", []);
    $("#ddExperience").dxSelectBox("instance").option("value", null);
    $("#tagAvailableDays").dxTagBox("instance").option("value", []);
    resetTimeSlots();
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

    const status = (skill.ApprovalStatus || skill.approvalStatus || '').toString().toLowerCase();
    const tempId = skill.TempId || skill.tempId;

    if (status === 'draft' && tempId) {
        editingTempId = tempId;
        editingUserSkillId = null;
    } else {
        editingUserSkillId = skill.UserSkillId || skill.userSkillId || null;
        editingTempId = null;
    }

    const fieldBox = $("#ddField").dxSelectBox("instance");
    const skillBox = $("#ddSkill").dxSelectBox("instance");
    const subBox = $("#ddSubSkill").dxTagBox("instance");
    const expBox = $("#ddExperience").dxSelectBox("instance");
    const daysBox = $("#tagAvailableDays").dxTagBox("instance");

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
    loadTimeSlotsFromSkill(skill);

    DevExpress.ui.notify("Loaded skill into the form. Adjust values and click 'Add Skill +' to save changes.", "info", 3000);
};

function proceedToVerification(skipSkills) {
    if (!skipSkills && allSkills.length === 0) {
        DevExpress.ui.notify("Please add at least one skill, or use Skip skills.", "warning", 3000);
        return;
    }

    if (!skipSkills && !cvFile) {
        DevExpress.ui.notify("Please upload your CV before proceeding", "warning", 3000);
        return;
    }

    const formData = new FormData();
    if (cvFile) formData.append('cv', cvFile);
    formData.append('portfolioUrl', $("#txtPortfolio").dxTextBox("instance").option("value") || '');
    formData.append('skipSkills', skipSkills ? 'true' : 'false');

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
            DevExpress.ui.notify("Error completing registration", "error", 2000);
        }
    });
}

function skipSkillsAndProceed() {
    DevExpress.ui.dialog.confirm(
        "Register without adding skills? You can ask for help after admin approves your account. You may add skills later from your profile to become a helper.",
        "Skip skills"
    ).done(function (ok) {
        if (ok) proceedToVerification(true);
    });
}