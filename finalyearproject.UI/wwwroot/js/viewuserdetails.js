// wwwroot/js/admin/viewuserdetails.js

$(function () {
    initializeUserDetailsPage();
});

function initializeUserDetailsPage() {
    const userId = window.userData.userId;
    const username = window.userData.username;
    const skills = window.userData.skills || [];

    // Initialize Skills DataGrid
    $("#skillsGrid").dxDataGrid({
        dataSource: skills,
        showBorders: true,
        showRowLines: true,
        columnAutoWidth: true,
        rowAlternationEnabled: true,
        columns: [
            {
                dataField: "FieldName",
                caption: "Field",
                width: 150
            },
            {
                dataField: "SkillName",
                caption: "Skill",
                width: 150
            },
            {
                dataField: "SubSkillName",
                caption: "Sub-Skill",
                width: 150
            },
            {
                dataField: "ExperienceLevel",
                caption: "Experience",
                width: 200,
                cellTemplate: function (container, options) {
                    const expLevels = {
                        1: 'Student / Final Year / Intern',
                        2: '0-3 Yrs',
                        3: '3-5 Yrs',
                        4: '5+ Yrs'
                    };
                    $("<span>")
                        .addClass("badge bg-info text-dark")
                        .text(expLevels[options.value])
                        .appendTo(container);
                }
            },
            {
                dataField: "AvailableDays",
                caption: "Available Days",
                width: 150
            },
            {
                caption: "Available Time",
                width: 150,
                calculateCellValue: function (rowData) {
                    const start = rowData.AvailableTimeStart ? rowData.AvailableTimeStart.substring(0, 5) : '';
                    const end = rowData.AvailableTimeEnd ? rowData.AvailableTimeEnd.substring(0, 5) : '';
                    return start && end ? `${start} - ${end}` : 'N/A';
                }
            }
        ],
        noDataText: "No skills added by this user."
    });

    // Initialize Approve Button
    $("#btnApprove").dxButton({
        text: "✓ Approve User",
        type: "success",
        width: 150,
        onClick: function () {
            approveUser(userId, username);
        }
    });

    // Initialize Reject Button
    $("#btnReject").dxButton({
        text: "✗ Reject User",
        type: "danger",
        width: 150,
        onClick: function () {
            rejectUser(userId, username);
        }
    });
}

function approveUser(userId, username) {
    const result = DevExpress.ui.dialog.confirm(
        `Approve user "${username}"? They will receive an approval email and can log in.`,
        "Confirm Approval"
    );

    result.done(function (dialogResult) {
        if (dialogResult) {
            $.post('/Admin/ApproveUser', { userId: userId }, function (data) {
                if (data.success) {
                    DevExpress.ui.notify(data.message, "success", 3000);
                    setTimeout(function () {
                        window.location.href = '/Admin/PendingUsers';
                    }, 2000);
                } else {
                    DevExpress.ui.notify(data.message, "error", 3000);
                }
            }).fail(function () {
                DevExpress.ui.notify("Error approving user", "error", 3000);
            });
        }
    });
}

function rejectUser(userId, username) {
    const result = DevExpress.ui.dialog.prompt(
        `Please provide a reason for rejecting "${username}":`,
        "Reject User"
    );

    result.done(function (reason) {
        if (reason) {
            $.post('/Admin/RejectUser', { userId: userId, reason: reason }, function (data) {
                if (data.success) {
                    DevExpress.ui.notify(data.message, "success", 3000);
                    setTimeout(function () {
                        window.location.href = '/Admin/PendingUsers';
                    }, 2000);
                } else {
                    DevExpress.ui.notify(data.message, "error", 3000);
                }
            }).fail(function () {
                DevExpress.ui.notify("Error rejecting user", "error", 3000);
            });
        }
    });
}