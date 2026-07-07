$(document).ready(function () {
    loadPendingSkills();
});

var experienceLevels = {
    1: 'Student / Final Year / Intern',
    2: '0-3 Yrs',
    3: '3-5 Yrs',
    4: '5+ Yrs'
};

function getSkillGridInstance() {
    var grid = $('#skillGrid');
    if (!grid.length) return null;
    try {
        return grid.dxDataGrid('instance');
    } catch (e) {
        return null;
    }
}

function setSkillGridData(rows) {
    var instance = getSkillGridInstance();
    if (instance) {
        instance.option('dataSource', rows);
        return;
    }
    buildSkillGrid(rows);
}

function groupPendingByUser(skillRows) {
    var map = {};
    skillRows.forEach(function (r) {
        var uid = r.userId || r.UserId;
        if (!uid) return;

        if (!map[uid]) {
            map[uid] = {
                userId: uid,
                username: r.username || r.Username || '',
                email: r.email || r.Email || '',
                cvPath: r.cvPath || r.CVPath || '',
                portfolioUrl: r.portfolioUrl || r.PortfolioUrl || '',
                pendingSkillCount: 0,
                latestSubmittedAt: null,
                skills: []
            };
        }

        map[uid].skills.push(r);
        map[uid].pendingSkillCount++;

        var submitted = r.skillSubmittedAt || r.SkillSubmittedAt;
        if (submitted) {
            var d = new Date(submitted);
            if (!map[uid].latestSubmittedAt || d > new Date(map[uid].latestSubmittedAt)) {
                map[uid].latestSubmittedAt = submitted;
            }
        }
    });

    return Object.values(map);
}

function skillPath(r) {
    var field = r.fieldName || r.FieldName || '';
    var skill = r.skillName || r.SkillName || '';
    var sub = r.subSkillName || r.SubSkillName || '';
    return field + ' > ' + skill + ' > ' + sub;
}

function openPendingSkillsModal(row) {
    var name = row.username || 'User';
    $('#pendingSkillsModalLabel').text('Pending skills — ' + name);
    $('#pendingSkillsModalBody').html('<div class="text-muted">Loading…</div>');

    var modalEl = document.getElementById('pendingSkillsModal');
    bootstrap.Modal.getOrCreateInstance(modalEl).show();

    var parts = [];
    parts.push('<div class="mb-3"><strong>User:</strong> ' + name + '</div>');
    parts.push('<div class="mb-2" style="color:#6b8fa8;">' + (row.email || '') + '</div>');

    if (row.cvPath) {
        parts.push('<div class="mb-3"><a href="' + row.cvPath + '" target="_blank" class="btn btn-sm btn-outline-info">View CV</a>');
        if (row.portfolioUrl) {
            parts.push(' <a href="' + row.portfolioUrl + '" target="_blank" class="btn btn-sm btn-outline-light">Portfolio</a>');
        }
        parts.push('</div>');
    }

    parts.push('<div class="mb-3"><strong>Skills waiting for approval (' + row.skills.length + '):</strong></div>');

    row.skills.forEach(function (s) {
        var id = s.userSkillId || s.UserSkillId;
        var exp = experienceLevels[s.experienceLevel || s.ExperienceLevel] || 'N/A';
        var days = s.availableDays || s.AvailableDays || '—';
        var start = (s.availableTimeStart || s.AvailableTimeStart || '').toString().substring(0, 5);
        var end = (s.availableTimeEnd || s.AvailableTimeEnd || '').toString().substring(0, 5);
        var when = s.skillSubmittedAt || s.SkillSubmittedAt;
        var whenText = when ? new Date(when).toLocaleString() : '—';

        parts.push('<div class="mb-3 p-3 rounded pending-skill-block" style="background:rgba(255,255,255,0.05);" data-skill-id="' + id + '">');
        parts.push('<strong>' + skillPath(s) + '</strong> — ' + whenText + '<br>');
        parts.push('<span class="badge text-bg-info mt-1">' + exp + '</span>');
        parts.push('<div class="small mt-1" style="color:#6b8fa8;">' + days + ' | ' + start + ' - ' + end + '</div>');
        parts.push('<div class="d-flex gap-2 mt-2">');
        parts.push('<button type="button" class="btn btn-sm btn-success btn-approve-skill">Approve</button>');
        parts.push('<button type="button" class="btn btn-sm btn-danger btn-reject-skill">Reject</button>');
        parts.push('</div></div>');
    });

    $('#pendingSkillsModalBody').html(parts.join(''));

    $('#pendingSkillsModalBody .pending-skill-block').each(function () {
        var block = $(this);
        var skillId = parseInt(block.attr('data-skill-id'), 10);
        var skillRow = row.skills.find(function (s) {
            return (s.userSkillId || s.UserSkillId) === skillId;
        });

        block.find('.btn-approve-skill').on('click', function () {
            approveSkill(skillId, skillRow, true);
        });
        block.find('.btn-reject-skill').on('click', function () {
            rejectSkill(skillId, skillRow, true);
        });
    });
}

function loadSkillStats() {
    return fetch('/Admin/GetSkillApprovalStats')
        .then(function (res) { return res.json(); })
        .then(function (stats) {
            stats = stats || {};
            $('#countPending').text(stats.pendingCount ?? 0);
            $('#countApproved').text(stats.approvedCount ?? 0);
            $('#countRejected').text(stats.rejectedCount ?? 0);
            return stats;
        });
}

function loadPendingSkills() {
    loadSkillStats();

    fetch('/Admin/GetPendingUserSkills')
        .then(function (res) {
            return res.json().then(function (body) {
                if (!res.ok) {
                    var msg = (body && (body.detail || body.error)) || ('HTTP ' + res.status);
                    throw new Error(msg);
                }
                return body;
            });
        })
        .then(function (rows) {
            if (!Array.isArray(rows)) rows = [];
            var grouped = groupPendingByUser(rows);
            setSkillGridData(grouped);
        })
        .catch(function (err) {
            console.error(err);
            var msg = err.message || String(err);
            if (msg.indexOf('E0009') === -1) {
                alert('Could not load pending skills:\n\n' + msg);
            }
            setSkillGridData([]);
        });
}

function buildSkillGrid(data) {
    var grid = $('#skillGrid');
    var existing = getSkillGridInstance();
    if (existing) {
        existing.dispose();
    }

    grid.dxDataGrid({
        dataSource: data,
        keyExpr: 'userId',
        showBorders: true,
        columnAutoWidth: true,
        wordWrapEnabled: true,
        noDataText: 'No skills waiting for approval.',
        paging: { pageSize: 15 },
        pager: { showInfo: true, showNavigationButtons: true },
        onRowClick: function (e) {
            openPendingSkillsModal(e.data);
        },
        columns: [
            {
                caption: 'Helper',
                width: 240,
                cellTemplate: function (container, options) {
                    var u = options.data;
                    var name = u.username || 'Unknown';
                    var initial = (name || 'U').charAt(0).toUpperCase();
                    container.html(
                        '<div style="display:flex;align-items:center;gap:10px;">' +
                        '<div style="width:32px;height:32px;border-radius:8px;background:linear-gradient(135deg,#00d2d2,#0077ff);display:flex;align-items:center;justify-content:center;font-weight:700;color:#fff;">' + initial + '</div>' +
                        '<div><div style="font-weight:600;">' + name + '</div>' +
                        '<div style="font-size:0.75rem;color:#6b8fa8;">' + (u.email || '') + '</div></div></div>'
                    );
                }
            },
            {
                caption: 'Pending skills',
                width: 130,
                cellTemplate: function (c, o) {
                    var n = o.data.pendingSkillCount || 0;
                    $(c).text(n + ' skill' + (n !== 1 ? 's' : ''));
                }
            },
            {
                caption: 'Last submitted',
                width: 170,
                cellTemplate: function (c, o) {
                    var v = o.data.latestSubmittedAt;
                    $(c).text(v ? new Date(v).toLocaleString() : '—');
                }
            },
            {
                caption: 'CV',
                width: 100,
                cellTemplate: function (c, o) {
                    var cv = o.data.cvPath;
                    if (!cv) {
                        $(c).html('<span style="color:#6b8fa8;font-size:0.8rem;">No CV</span>');
                        return;
                    }
                    $('<a href="' + cv + '" target="_blank" class="btn btn-sm btn-outline-success">View CV</a>').appendTo(c);
                }
            },
            {
                caption: '',
                width: 90,
                cellTemplate: function (container, options) {
                    var btn = $('<button type="button" class="btn btn-sm btn-outline-info">View</button>');
                    btn.on('click', function (ev) {
                        ev.stopPropagation();
                        openPendingSkillsModal(options.data);
                    });
                    $(container).append(btn);
                }
            }
        ]
    });
}

function approveSkill(userSkillId, row, fromModal) {
    if (!confirm('Approve skill: ' + skillPath(row) + '?')) return;
    $.post('/Admin/ApproveUserSkill', { userSkillId: userSkillId }, function (res) {
        if (res.success) {
            if (fromModal) {
                bootstrap.Modal.getInstance(document.getElementById('pendingSkillsModal'))?.hide();
            }
            loadPendingSkills();
        } else {
            alert(res.message || 'Could not approve');
        }
    });
}

function rejectSkill(userSkillId, row, fromModal) {
    var reason = prompt('Reason for rejection (shown to user):', 'CV or skill details did not match our verification requirements.');
    if (reason === null) return;
    $.post('/Admin/RejectUserSkill', { userSkillId: userSkillId, reason: reason }, function (res) {
        if (res.success) {
            if (fromModal) {
                bootstrap.Modal.getInstance(document.getElementById('pendingSkillsModal'))?.hide();
            }
            loadPendingSkills();
        } else {
            alert(res.message || 'Could not reject');
        }
    });
}
