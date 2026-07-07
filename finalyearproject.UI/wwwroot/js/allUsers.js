$(function () {
    fetch('/Admin/GetAllUsers')
        .then(function (r) { return r.json(); })
        .then(function (users) {
            if (!Array.isArray(users)) users = [];

            users.sort(function (a, b) {
                var da = a.createdAt ? new Date(a.createdAt) : new Date(0);
                var db = b.createdAt ? new Date(b.createdAt) : new Date(0);
                return db - da;
            });

            $('#allUsersGrid').dxDataGrid({
                dataSource: users,
                keyExpr: 'userId',
                showBorders: true,
                rowAlternationEnabled: true,
                columnAutoWidth: true,
                paging: { pageSize: 25 },
                pager: { showInfo: true, showNavigationButtons: true, showPageSizeSelector: true },
                onRowClick: function (e) {
                    openUserSkillsPortfolioModal(e.data);
                },
                columns: [
                    {
                        caption: 'User',
                        width: 260,
                        cellTemplate: function (container, options) {
                            var u = options.data;
                            var initial = (u.username || '?').charAt(0).toUpperCase();
                            container.html(
                                '<div style="display:flex;align-items:center;gap:10px;">' +
                                '<div style="width:32px;height:32px;border-radius:8px;background:linear-gradient(135deg,#7c3aed,#a78bfa);display:flex;align-items:center;justify-content:center;font-weight:700;color:#fff;">' + initial + '</div>' +
                                '<div><div style="font-weight:600;">' + (u.username || '') + '</div>' +
                                '<div style="font-size:0.75rem;color:#6b8fa8;">ID: ' + (u.userId || '') + '</div></div></div>'
                            );
                        }
                    },
                    {
                        dataField: 'email',
                        caption: 'Email',
                        minWidth: 200
                    },
                    {
                        dataField: 'phoneNumber',
                        caption: 'Phone',
                        width: 130
                    },
                    {
                        dataField: 'createdAt',
                        caption: 'Registered',
                        dataType: 'date',
                        format: 'dd MMM yyyy',
                        width: 120
                    },
                    {
                        caption: 'Skills',
                        width: 110,
                        cellTemplate: function (c, o) {
                            var n = o.data.skillCount ?? 0;
                            if (n === 0) {
                                $(c).html('<span style="color:#6b8fa8;">No skills</span>');
                            } else {
                                $(c).html('<span style="color:#48bb78;font-weight:600;">' + n + '</span>');
                            }
                        }
                    },
                    {
                        caption: 'Account',
                        width: 110,
                        cellTemplate: function (c, o) {
                            var u = o.data;
                            var html;
                            if (u.isApprovedByAdmin) {
                                html = '<span style="color:#48bb78;font-size:0.75rem;font-weight:700;">Active</span>';
                            } else if (u.isRejected) {
                                html = '<span style="color:#f56565;font-size:0.75rem;font-weight:700;">Rejected</span>';
                            } else {
                                html = '<span style="color:#f6ad55;font-size:0.75rem;font-weight:700;">Pending</span>';
                            }
                            $(c).html(html);
                        }
                    },
                    {
                        caption: 'CV',
                        width: 90,
                        cellTemplate: function (c, o) {
                            var cv = o.data.cvPath;
                            if (!cv) {
                                $(c).text('—');
                                return;
                            }
                            $('<a href="' + cv + '" target="_blank" class="btn btn-sm btn-outline-light">CV</a>').appendTo(c);
                        }
                    },
                    {
                        caption: '',
                        width: 90,
                        cellTemplate: function (container, options) {
                            var btn = $('<button type="button" class="btn btn-sm btn-outline-info">View</button>');
                            btn.on('click', function (ev) {
                                ev.stopPropagation();
                                openUserSkillsPortfolioModal(options.data);
                            });
                            $(container).append(btn);
                        }
                    }
                ]
            });
        })
        .catch(function () {
            alert('Could not load users.');
        });
});

/** Modal list style like Recent Changes — one block per skill. */
function openUserSkillsPortfolioModal(user) {
    var name = user.username || 'User';
    $('#skillsModalLabel').text('Skills portfolio — ' + name);
    $('#skillsModalBody').html('<div class="text-muted">Loading skills…</div>');

    bootstrap.Modal.getOrCreateInstance(document.getElementById('skillsModal')).show();

    $.getJSON('/AdminModal/UserSkills', { userId: user.userId })
        .done(function (skills) {
            if (!Array.isArray(skills)) skills = [];

            var levels = {
                1: 'Student / Final Year / Intern',
                2: '0-3 Yrs',
                3: '3-5 Yrs',
                4: '5+ Yrs'
            };

            var parts = [];
            parts.push('<div class="mb-3"><strong>User:</strong> ' + name + '</div>');
            parts.push('<div class="mb-2" style="color:#6b8fa8;">' + (user.email || '') + '</div>');

            if (user.cvPath) {
                parts.push('<div class="mb-3"><a href="' + user.cvPath + '" target="_blank" class="btn btn-sm btn-outline-info">View CV</a>');
                if (user.portfolioUrl) {
                    parts.push(' <a href="' + user.portfolioUrl + '" target="_blank" class="btn btn-sm btn-outline-light">Portfolio</a>');
                }
                parts.push('</div>');
            }

            parts.push('<div class="mb-3"><strong>Skills (' + skills.length + '):</strong></div>');

            if (!skills.length) {
                parts.push('<div class="text-muted">This user has not added any skills yet.</div>');
            } else {
                skills.forEach(function (s) {
                    var field = s.fieldName || s.FieldName || '';
                    var skill = s.skillName || s.SkillName || '';
                    var sub = s.subSkillName || s.SubSkillName || '';
                    var path = field + ' > ' + skill + (sub ? ' > ' + sub : '');
                    var exp = levels[s.experienceLevel || s.ExperienceLevel] || 'N/A';
                    var status = (s.approvalStatus || s.ApprovalStatus || '—').toString();
                    var days = s.availableDays || s.AvailableDays || '—';
                    var start = (s.availableTimeStart || s.AvailableTimeStart || '').toString().substring(0, 5);
                    var end = (s.availableTimeEnd || s.AvailableTimeEnd || '').toString().substring(0, 5);

                    parts.push('<div class="mb-2 p-2 rounded" style="background:rgba(255,255,255,0.05);">');
                    parts.push('<strong>' + path + '</strong><br>');
                    parts.push('<span class="badge text-bg-secondary me-1">' + status + '</span>');
                    parts.push('<span class="badge text-bg-info">' + exp + '</span>');
                    parts.push('<div class="small mt-1" style="color:#6b8fa8;">' + days + ' | ' + start + ' - ' + end + '</div>');
                    parts.push('</div>');
                });
            }

            $('#skillsModalBody').html(parts.join(''));
        })
        .fail(function () {
            $('#skillsModalBody').html('<div class="text-danger">Failed to load skills.</div>');
        });
}
