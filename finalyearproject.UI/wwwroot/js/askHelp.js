$(document).ready(function () {
    // 1. Initialize Dropdowns
    $("#selectField").dxSelectBox({
        dataSource: '/Skills/GetFields', // Update path to your field endpoint
        displayExpr: "fieldName",
        valueExpr: "fieldId",
        placeholder: "Choose Field...",
        onValueChanged: function (e) {
            $("#selectSkill").dxSelectBox("instance").option("dataSource", '/Skills/GetSkills?fieldId=' + e.value);
            $("#selectSkill").dxSelectBox("instance").option("value", null);
        }
    });

    $("#selectSkill").dxSelectBox({
        displayExpr: "skillName",
        valueExpr: "skillId",
        placeholder: "Choose Skill...",
        onValueChanged: function (e) {
            $("#selectSubSkill").dxSelectBox("instance").option("dataSource", '/Skills/GetSubSkills?skillId=' + e.value);
        }
    });

    $("#selectSubSkill").dxSelectBox({
        displayExpr: "subSkillName",
        valueExpr: "subSkillId",
        placeholder: "Choose Sub-Skill..."
    });

    // 2. Search Button
    $("#btnSearch").dxButton({
        text: "Search Helpers",
        type: "default",
        icon: "find",
        onClick: function () {
            const fId = $("#selectField").dxSelectBox("instance").option("value");
            const sId = $("#selectSkill").dxSelectBox("instance").option("value");
            const subId = $("#selectSubSkill").dxSelectBox("instance").option("value");

            if (!subId) {
                DevExpress.ui.notify("Please select a sub-skill", "error", 2000);
                return;
            }

            $("#resultsCard").fadeIn();
            loadHelperGrid(fId, sId, subId);
        }
    });
});

function loadHelperGrid(fId, sId, subId) {
    $("#helperGrid").dxDataGrid({
        dataSource: `/User/GetRankedHelpers?fieldId=${fId}&skillId=${sId}&subSkillId=${subId}`,
        showBorders: true,
        rowAlternationEnabled: true,
        columnAutoWidth: true,
        loadPanel: { enabled: true },
        columns: [
            {
                caption: "Rank",
                width: 70,
                cellTemplate: function (c, o) { c.text(o.rowIndex + 1); }
            },
            { dataField: "username", caption: "Helper Name", cssClass: "fw-bold" },
            {
                caption: "Experience Level",
                cellTemplate: function (container, options) {
                    // Accessing the first skill match from the ParsedSkills array returned by Controller
                    const skill = options.data.parsedSkills.find(s => s.subSkillId === subId);
                    const levels = { 1: 'Intern', 2: 'Junior', 3: 'Mid-Level', 4: 'Senior' };
                    $('<span class="badge bg-primary">')
                        .text(levels[skill?.experienceLevel] || 'Expert')
                        .appendTo(container);
                }
            },
            {
                type: "buttons",
                width: 150,
                buttons: [{
                    text: "Request Help",
                    hint: "Start a session",
                    onClick: function (e) {
                        startSession(e.row.data.userId);
                    }
                }]
            }
        ]
    });
}

function startSession(helperId) {
    alert("Requesting help from User ID: " + helperId);
}