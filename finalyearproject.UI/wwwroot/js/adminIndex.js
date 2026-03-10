// Admin dashboard scripts (DevExtreme only, no tables)

function openRecentChangeModal(row) {
    var userId = row.userId || row.UserId || 0;
    var name = row.username || row.Username || 'Unknown';

    $('#recentChangeModalLabel').text('Changes by ' + name);
    $('#recentChangeModalBody').html('<div class="text-muted">Loading changes…</div>');

    var modalEl = document.getElementById('recentChangeModal');
    var modal = bootstrap.Modal.getOrCreateInstance(modalEl);
    modal.show();

    $.getJSON('/AdminModal/UserChangeDetails', { userId: userId })
        .done(function (d) {
            var cvPath = d.cvPath || '';
            var portfolio = d.portfolioUrl || '';
            var changes = d.changes || [];

            var parts = [];
            parts.push('<div class="mb-3"><strong>User:</strong> ' + (d.username || name) + '</div>');
            parts.push('<div class="mb-3"><strong>Recent changes:</strong></div>');

            changes.forEach(function (c) {
                var action = c.actionType || '';
                var when = c.changedAt ? new Date(c.changedAt).toLocaleString() : '—';

                if (action === 'UPDATE_DOCUMENTS' || action === 'updateDocuments') {
                    parts.push('<div class="mb-2 p-2 rounded" style="background:rgba(255,255,255,0.05);">');
                    parts.push('<strong>CV & Portfolio updated</strong> — ' + when + '<br>');
                    if (cvPath) parts.push('<a href="' + cvPath + '" target="_blank" class="btn btn-sm btn-outline-info mt-1">Download CV</a> ');
                    if (portfolio) parts.push('<a href="' + portfolio + '" target="_blank" rel="noopener">Portfolio</a>');
                    parts.push('</div>');
                } else {
                    var f = c.fieldName || '', s = c.skillName || '', sub = c.subSkillName || '';
                    var sd = (f && s) ? (f + ' > ' + s + (sub ? ' > ' + sub : '')) : 'Skill updated';
                    parts.push('<div class="mb-2 p-2 rounded" style="background:rgba(255,255,255,0.05);">');
                    parts.push('<strong>' + sd + '</strong> — ' + when);
                    parts.push('</div>');
                }
            });

            if (changes.length === 0) {
                parts.push('<div class="text-muted">No changes found.</div>');
            }

            $('#recentChangeModalBody').html(parts.join(''));
        })
        .fail(function () {
            $('#recentChangeModalBody').html('<div class="text-danger">Failed to load changes.</div>');
        });
}

function openSkillsModal(user) {
    var title = 'Skills Portfolio — ' + (user.username || '');
    $('#skillsModalLabel').text(title);

    var modalEl = document.getElementById('skillsModal');
    var modal = bootstrap.Modal.getOrCreateInstance(modalEl);
    modal.show();

    $('#skillsModalBody').html('<div id="skillsGrid" style="min-height:200px;"></div>');

    $.getJSON('/AdminModal/UserSkills', { userId: user.userId })
        .done(function (skills) {
            if (!Array.isArray(skills)) skills = [];

            var levels = {
                1: 'Student / Final Year / Intern',
                2: '0-3 Yrs',
                3: '3-5 Yrs',
                4: '5+ Yrs'
            };

            $('#skillsGrid').dxDataGrid({
                dataSource: skills,
                keyExpr: 'UserSkillId',
                showBorders: true,
                rowAlternationEnabled: true,
                columnAutoWidth: true,
                paging: { pageSize: 10 },
                pager: {
                    showInfo: true,
                    showNavigationButtons: true
                },
                columns: [
                    { dataField: 'FieldName', caption: 'Field' },
                    { dataField: 'SkillName', caption: 'Skill' },
                    { dataField: 'SubSkillName', caption: 'Sub-skill' },
                    {
                        dataField: 'ExperienceLevel',
                        caption: 'Experience',
                        width: 130,
                        cellTemplate: function (c, o) {
                            var text = levels[o.value] || 'N/A';
                            $('<span>').text(text).appendTo(c);
                        }
                    },
                    { dataField: 'AvailableDays', caption: 'Days', width: 130 },
                    {
                        caption: 'Time',
                        width: 120,
                        cellTemplate: function (c, o) {
                            var start = (o.data.AvailableTimeStart || '').toString();
                            var end = (o.data.AvailableTimeEnd || '').toString();
                            var value = (start && end) ? (start + ' - ' + end) : '—';
                            $('<span>').text(value).appendTo(c);
                        }
                    },
                    {
                        caption: 'Actions',
                        width: 120,
                        cellTemplate: function (c, o) {
                            var id = o.data.UserSkillId || o.data.userSkillId;
                            if (!id) {
                                $('<span>').text('—').appendTo(c);
                                return;
                            }
                            var btn = $('<button type="button" class="btn btn-sm btn-outline-danger">Remove</button>');
                            btn.on('click', function () {
                                if (!confirm('Remove this skill from the user profile?')) return;
                                $.post('/AdminModal/RemoveUserSkill', { userSkillId: id })
                                    .done(function (res) {
                                        if (res && res.success) {
                                            DevExpress.ui.notify('Skill removed', 'success', 2000);
                                            openSkillsModal(user);
                                        } else {
                                            DevExpress.ui.notify((res && res.message) || 'Could not remove skill', 'error', 2500);
                                        }
                                    })
                                    .fail(function () {
                                        DevExpress.ui.notify('Error removing skill', 'error', 2500);
                                    });
                            });
                            $(c).append(btn);
                        }
                    }
                ]
            });
        })
        .fail(function () {
            $('#skillsGrid').html('<div class="text-danger">Failed to load skills for this user.</div>');
        });
}

// Load help statistics for admin dashboard
function loadHelpStatistics() {
    $.getJSON('/Admin/GetHelpStatistics').done(function (stats) {
        var dashHelpers = $('#dashTotalHelpers');
        if (dashHelpers.length) dashHelpers.text(stats.TotalHelpers ?? 0);
        var dashHelps = $('#dashTotalHelps');
        if (dashHelps.length) dashHelps.text(stats.TotalHelpsGiven ?? 0);
        var dashNotHelped = $('#dashRequestsNotHelped');
        if (dashNotHelped.length) dashNotHelped.text(stats.RequestsNotHelped ?? 0);

        var helpers = stats.HelperBreakdown || [];
        var helpersEl = $('#helpersGrid');
        if (helpersEl.length) helpersEl.dxDataGrid({
            dataSource: helpers,
            keyExpr: 'UserId',
            showBorders: true,
            rowAlternationEnabled: true,
            paging: { pageSize: 10 },
            pager: { showInfo: true, showNavigationButtons: true },
            columns: [
                { dataField: 'Username', caption: 'User' },
                { dataField: 'HelpsGiven', caption: 'Helps given', dataType: 'number' }
            ]
        });

        var notHelped = stats.UsersWhoDidNotHelp || [];
        var notHelpedEl = $('#usersNotHelpedGrid');
        if (notHelpedEl.length) notHelpedEl.dxDataGrid({
            dataSource: notHelped,
            keyExpr: 'UserId',
            showBorders: true,
            rowAlternationEnabled: true,
            paging: { pageSize: 10 },
            pager: { showInfo: true, showNavigationButtons: true },
            columns: [
                { dataField: 'Username', caption: 'User' },
                { dataField: 'RequestsReceived', caption: 'Requests received', dataType: 'number' },
                { dataField: 'Accepted', caption: 'Accepted', dataType: 'number', width: 90 },
                { dataField: 'Completed', caption: 'Completed', dataType: 'number', width: 100 },
                { dataField: 'NotCompleted', caption: 'Not completed', dataType: 'number', width: 120 },
                { dataField: 'Rejected', caption: 'Rejected', dataType: 'number', width: 90 },
                { dataField: 'Withdrawn', caption: 'Withdrawn', dataType: 'number', width: 100 },
                { dataField: 'Pending', caption: 'Pending', dataType: 'number', width: 90 }
            ]
        });
    });
}

// Dashboard counts (and optionally recent users + recent changes grids if elements exist)
$(function () {
    loadHelpStatistics();

    fetch('/Admin/GetPendingUsers')
        .then(function (res) { return res.json(); })
        .then(function (users) {
            if (!Array.isArray(users)) users = [];

            var pending = 0, approved = 0, rejected = 0;
            users.forEach(function (u) {
                if (u.isApprovedByAdmin) approved++;
                else if (u.isRejected) rejected++;
                else pending++;
            });

            $('#dashPending').text(pending);
            $('#dashApproved').text(approved);
            $('#dashRejected').text(rejected);

            var recentUsersEl = $('#recentUsersGrid');
            if (recentUsersEl.length === 0) return; // No grid on dashboard; counts are updated above

            users.sort(function (a, b) {
                var da = a.createdAt ? new Date(a.createdAt) : new Date(0);
                var db = b.createdAt ? new Date(b.createdAt) : new Date(0);
                return db - da;
            });

            var recent = users.slice(0, 20);

            recentUsersEl.dxDataGrid({
                dataSource: recent,
                keyExpr: 'userId',
                showBorders: true,
                rowAlternationEnabled: true,
                paging: { pageSize: 20 },
                pager: { showInfo: true, showNavigationButtons: true },
                columns: [
                    {
                        caption: 'User',
                        width: 260,
                        cellTemplate: function (container, options) {
                            var u = options.data;
                            var initial = (u.username || '?').charAt(0).toUpperCase();
                            var html =
                                '<div style="display:flex;align-items:center;gap:10px;">' +
                                '<div style="width:32px;height:32px;border-radius:8px;background:linear-gradient(135deg,#00d2d2,#0077ff);display:flex;align-items:center;justify-content:center;font-weight:800;color:#fff;">' +
                                initial +
                                '</div>' +
                                '<div>' +
                                '<div style="font-weight:700;color:#e8f4f8;">' + (u.username || '') + '</div>' +
                                '<div style="font-size:0.78rem;color:#6b8fa8;">' + (u.email || '') + '</div>' +
                                '</div>' +
                                '</div>';
                            $(container).html(html);
                        }
                    },
                    {
                        dataField: 'phoneNumber',
                        caption: 'Phone',
                        width: 140
                    },
                    {
                        dataField: 'createdAt',
                        caption: 'Registered',
                        dataType: 'date',
                        format: 'dd MMM yyyy',
                        width: 130
                    },
                    {
                        caption: 'Status',
                        width: 120,
                        cellTemplate: function (c, o) {
                            var u = o.data;
                            var html;
                            if (u.isApprovedByAdmin)
                                html = '<span style="background:rgba(72,187,120,0.15);color:#48bb78;border:1px solid rgba(72,187,120,0.3);padding:3px 9px;border-radius:20px;font-size:0.72rem;font-weight:700;">Approved</span>';
                            else if (u.isRejected)
                                html = '<span style="background:rgba(245,101,101,0.15);color:#f56565;border:1px solid rgba(245,101,101,0.3);padding:3px 9px;border-radius:20px;font-size:0.72rem;font-weight:700;">Rejected</span>';
                            else
                                html = '<span style="background:rgba(246,173,85,0.15);color:#f6ad55;border:1px solid rgba(246,173,85,0.3);padding:3px 9px;border-radius:20px;font-size:0.72rem;font-weight:700;">Pending</span>';
                            $(c).html(html);
                        }
                    },
                    {
                        caption: 'CV',
                        width: 90,
                        cellTemplate: function (c, o) {
                            var u = o.data;
                            var path = u.cvPath;
                            if (!path) {
                                $('<span>').text('—').appendTo(c);
                                return;
                            }
                            var link = $('<a>')
                                .attr('href', path)
                                .attr('target', '_blank')
                                .addClass('btn btn-sm btn-outline-light')
                                .text('CV');
                            $(c).append(link);
                        }
                    },
                    {
                        caption: 'Skills',
                        width: 110,
                        cellTemplate: function (c, o) {
                            var u = o.data;
                            var btn = $('<button type="button" class="btn btn-sm btn-outline-info">View</button>');
                            btn.on('click', function () { openSkillsModal(u); });
                            $(c).append(btn);
                        }
                    }
                ]
            });
        });

    var recentChangesEl = $('#recentChangesGrid');
    if (recentChangesEl.length > 0) {
    // Recent user skill / CV changes grid for admin (one row per user)
    recentChangesEl.dxDataGrid({
        dataSource: "/Admin/GetRecentUserChanges",
        keyExpr: "userId",
        showBorders: true,
        rowAlternationEnabled: true,
        columnAutoWidth: true,
        paging: { pageSize: 20 },
        pager: {
            showInfo: true,
            showNavigationButtons: true
        },
        onRowClick: function (e) {
            openRecentChangeModal(e.data);
        },
        columns: [
            {
                caption: "User",
                width: 220,
                cellTemplate: function (container, options) {
                    var u = options.data;
                    var name = u.username || u.Username || 'Unknown';
                    var initial = (name || 'U').charAt(0).toUpperCase();
                    var html =
                        '<div style="display:flex;align-items:center;gap:10px;">' +
                        '<div style="width:28px;height:28px;border-radius:8px;background:linear-gradient(135deg,#7c3aed,#a78bfa);display:flex;align-items:center;justify-content:center;font-weight:700;color:#fff;font-size:0.8rem;">' +
                        initial +
                        '</div>' +
                        '<div>' +
                        '<div style="font-weight:600;">' + name + '</div>' +
                        '<div style="font-size:0.75rem;color:#6b8fa8;">ID: ' + (u.userId || u.UserId || '') + '</div>' +
                        '</div>' +
                        '</div>';
                    $(container).html(html);
                }
            },
            {
                caption: "Changes",
                width: 140,
                cellTemplate: function (c, o) {
                    var n = o.data.changeCount || 0;
                    var txt = n + ' change' + (n !== 1 ? 's' : '');
                    $(c).text(txt);
                }
            },
            {
                caption: "Last updated",
                width: 170,
                cellTemplate: function (c, o) {
                    var v = o.data.latestChangedAt || o.data.latestchangedat;
                    var txt = v ? new Date(v).toLocaleString() : '—';
                    $(c).text(txt);
                }
            },
            {
                caption: "",
                width: 90,
                cellTemplate: function (container, options) {
                    var btn = $('<button type="button" class="btn btn-sm btn-outline-info">View</button>');
                    btn.on('click', function (ev) {
                        ev.stopPropagation();
                        openRecentChangeModal(options.data);
                    });
                    $(container).append(btn);
                }
            }
        ]
    });
    }
});
