$(document).ready(function () {

    buildGrid([]);
    loadUsers();
});

function loadUsers() {
    fetch('/Admin/GetPendingUsers')
        .then(res => res.json())
        .then(users => {
            if (!Array.isArray(users)) users = [];

            // Counts for the top cards
            var pendingCount = 0, approvedCount = 0, rejectedCount = 0;
            users.forEach(function (u) {
                if (u.isApprovedByAdmin) approvedCount++;
                else if (u.isRejected) rejectedCount++;
                else pendingCount++;
            });

            $('#countPending').text(pendingCount);
            $('#countApproved').text(approvedCount);
            $('#countRejected').text(rejectedCount);

            // Only show those waiting for approval in the grid (ONE row per user)
            var pendingOnly = users.filter(u => !u.isApprovedByAdmin && !u.isRejected);

            var gridElement = $('#userGrid');
            if (gridElement.length > 0 && gridElement.dxDataGrid('instance')) {
                gridElement.dxDataGrid('instance').option('dataSource', pendingOnly);
            } else {
                buildGrid(pendingOnly); 
            }
        });
}

function openSkillsModal(user) {
    var title = 'Skills Portfolio — ' + (user.username || '');
    $('#skillsModalLabel').text(title);
    $('#skillsModalBody').html('<div class="text-muted">Loading skills...</div>');

    var modalEl = document.getElementById('skillsModal');
    var modal = bootstrap.Modal.getOrCreateInstance(modalEl);
    modal.show();

    $.getJSON('/AdminModal/UserSkills', { userId: user.userId })
        .done(function (skills) {
            if (!Array.isArray(skills)) skills = [];

            var levels = {
                1: 'Student / Final Year / Intern',
                2: '0-3 Yrs',
                3: '3-5 Yrs',
                4: '5+ Yrs'
            };

            var html = '';
            html += '<div class="mb-2" style="color:#6b8fa8;">Total skills: <b style="color:#e8f4f8;">' + skills.length + '</b></div>';

            if (!skills.length) {
                html += '<div class="text-muted">No skills added by this user.</div>';
            } else {
                html += '<div class="table-responsive">';
                html += '<table class="table table-dark table-striped align-middle mb-0">';
                html += '<thead><tr>';
                html += '<th style="white-space:nowrap;">Field</th>';
                html += '<th style="white-space:nowrap;">Skill</th>';
                html += '<th style="white-space:nowrap;">Sub-skill</th>';
                html += '<th style="white-space:nowrap;">Experience</th>';
                html += '<th style="white-space:nowrap;">Days</th>';
                html += '<th style="white-space:nowrap;">Time</th>';
                html += '</tr></thead><tbody>';

                skills.forEach(function (s) {
                    var expText = levels[s.ExperienceLevel] || 'N/A';
                    var field = s.FieldName || '—';
                    var skill = s.SkillName || '—';
                    var sub = s.SubSkillName || '—';
                    var days = s.AvailableDays || '—';
                    var start = (s.AvailableTimeStart || '').toString();
                    var end = (s.AvailableTimeEnd || '').toString();
                    var time = (start && end) ? (start + ' - ' + end) : '—';

                    html += '<tr>';
                    html += '<td>' + field + '</td>';
                    html += '<td>' + skill + '</td>';
                    html += '<td>' + sub + '</td>';
                    html += '<td><span class="badge text-bg-info">' + expText + '</span></td>';
                    html += '<td>' + days + '</td>';
                    html += '<td style="white-space:nowrap;">' + time + '</td>';
                    html += '</tr>';
                });

                html += '</tbody></table></div>';
            }

            $('#skillsModalBody').html(html);
        })
        .fail(function () {
            $('#skillsModalBody').html('<div class="text-danger">Failed to load skills for this user.</div>');
        });
}

function buildGrid(data) {
    $('#userGrid').dxDataGrid({
        dataSource: data,
        keyExpr: 'userId',
        showBorders: true,
        columnAutoWidth: true,
        wordWrapEnabled: true,
        paging: { pageSize: 20 },
        pager: { showInfo: true, showNavigationButtons: true },
        columns: [
            {
                caption: 'User Registration',
                width: 200,
                cellTemplate: function (container, options) {
                    var u = options.data;
                    container.html(
                        '<b>' + u.username + '</b><br/>' +
                        '<small style="color:#6b8fa8;">' + u.email + '</small><br/>' +
                        '<small>' + u.phoneNumber + '</small>'
                    );
                }
            },
            {
                caption: 'Skills Portfolio',
                width: 250,
                cellTemplate: function (container, options) {
                    var u = options.data;
                    var count = u.skillCount || 0;

                    var wrap = $('<div style="font-size:12px;"></div>');
                    wrap.append('<div><b>Skills:</b> <span style="color:#00d2d2;font-weight:800;">' + count + '</span></div>');

                    var btn = $('<button type="button" class="btn btn-sm btn-outline-info mt-2">View</button>');
                    btn.on('click', function () { openSkillsModal(u); });
                    wrap.append(btn);

                    wrap.appendTo(container);
                }
            },
            {
                caption: 'Files & Links',
                width: 150,
                cellTemplate: function (container, options) {
                    var u = options.data;
                    // Link to the CV file
                    if (u.cvPath) {
                        $('<a href="' + u.cvPath + '" target="_blank" style="display:block;color:#48bb78;font-weight:bold;margin-bottom:5px;">📄 View CV</a>').appendTo(container);
                    }
                    // Link to the Portfolio URL
                    if (u.portfolioUrl) {
                        $('<a href="' + u.portfolioUrl + '" target="_blank" style="display:block;color:#0077ff;font-weight:bold;">🔗 Portfolio</a>').appendTo(container);
                    } else {
                        $('<span style="color:gray;font-size:11px;">No Portfolio Link</span>').appendTo(container);
                    }
                }
            },
            {
                caption: 'Actions',
                width: 190,
                cellTemplate: function (container, options) {
                    var u = options.data;
                    var wrap = $('<div></div>').css({
                        display: 'flex',
                        gap: '8px',
                        'white-space': 'nowrap',
                        'align-items': 'center'
                    });

                    $('<button style="background:#48bb78;color:white;border:none;padding:8px 12px;border-radius:6px;cursor:pointer;font-weight:bold;flex:1;">Approve</button>')
                        .on('click', function () { doApprove(u); }).appendTo(wrap);

                    $('<button style="background:#f56565;color:white;border:none;padding:8px 12px;border-radius:6px;cursor:pointer;flex:1;">Reject</button>')
                        .on('click', function () { doReject(u); }).appendTo(wrap);

                    wrap.appendTo(container);
                }
            }
        ]
    });
}

function doApprove(user) {
    if (!confirm("Approve " + user.username + "?")) return;
    $.post('/Admin/ApproveUser', { userId: user.userId }, function (res) {
        if (res.success) { loadUsers(); } else { alert(res.message); }
    });
}

function doReject(user) {
    var reason = prompt("Enter reason for rejection:");
    if (!reason) return;
    $.post('/Admin/RejectUser', { userId: user.userId, reason: reason }, function (res) {
        if (res.success) { loadUsers(); } else { alert(res.message); }
    });
}