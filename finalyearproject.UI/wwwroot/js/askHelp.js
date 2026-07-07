// Ask Help page logic (kept in wwwroot for clean MVC views)
(function () {
    const levelMap = { 1: 'Student / Final Year / Intern', 2: '0-3 Yrs', 3: '3-5 Yrs', 4: '5+ Yrs' };
    const levelClass = { 1: 'level-1', 2: 'level-2', 3: 'level-3', 4: 'level-4' };

    let selectedSubSkillId = null;
    let lastSearch = { fieldId: null, skillId: null, subSkillId: null, timeStart: '', timeEnd: '', day: '' };
    let requestAttemptedHelpers = [];
    let isRequestPending = false;

    var busyCopy = {
        helperActive: {
            notify: 'You are helping another user right now. Finish that help session from Help Inbox before asking for help yourself.',
            badge: 'Finish helping first',
            body: '<p><strong>You are currently helping another user.</strong><br>Finish that session from <strong>Help Inbox</strong>. After it is completed, you can search and ask for help from someone else.</p>'
        },
        seekerPending: {
            notify: 'You already have a request waiting for a response. Open My Requests to cancel it if you want to ask someone else.',
            badge: 'Waiting on a response',
            body: '<p><strong>You already have a help request waiting for a helper.</strong><br>Wait for a response, or cancel the pending request under <strong>My Requests</strong>. After that, you can search and request help from another helper.</p>'
        },
        seekerActive: {
            notify: 'Complete your help session with your current helper before asking someone else. Use Help Inbox or My Requests to finish the session.',
            badge: 'Finish current session first',
            body: '<p><strong>Complete your help session with your current helper before asking someone else.</strong><br>Finish the session from <strong>Help Inbox</strong> or <strong>My Requests</strong>. Then you can search and request help from another helper.</p>'
        },
        other: {
            notify: 'Complete or cancel your current help first — then you can ask help from someone else. Open My Requests to manage it.',
            badge: 'Complete current help first',
            body: '<p><strong>Complete your current help before asking someone else.</strong><br>Finish the session, or cancel a pending request under <strong>My Requests</strong>. After that, you can search and request help from another helper.</p>'
        }
    };

    function getBusyCopy(reason) {
        if (reason && busyCopy[reason]) return busyCopy[reason];
        return busyCopy.other;
    }

    function setAllRequestButtonsEnabled(enabled) {
        const grid = document.getElementById('helpersGrid');
        if (!grid) return;
        grid.querySelectorAll('.request-btn').forEach(function (b) {
            b.disabled = !enabled;
            if (enabled) b.textContent = 'Request Help';
        });
    }

    function showStatus(icon, text) {
        const banner = document.getElementById('statusBanner');
        if (!banner) return;
        document.getElementById('statusIcon').textContent = icon;
        document.getElementById('statusText').textContent = text;
        banner.style.display = 'flex';
    }

    function hideStatus() {
        const banner = document.getElementById('statusBanner');
        if (banner) banner.style.display = 'none';
    }

    function findHelpers(fId, sId, subId, timeStart, timeEnd, day) {
        showStatus('🔍', 'Searching for qualified helpers...');
        const resultsSection = document.getElementById('resultsSection');
        if (resultsSection) resultsSection.style.display = 'none';

        const q = new URLSearchParams({ fieldId: fId, skillId: sId, subSkillId: subId });
        if (timeStart) q.set('timeStart', timeStart);
        if (timeEnd) q.set('timeEnd', timeEnd);
        if (day) q.set('availableDay', day);

        fetch('/User/GetRankedHelpers?' + q.toString())
            .then(r => r.json())
            .then(function (data) {
                hideStatus();
                var helpers = [];
                var seekerBusy = false;
                var busyReason = '';
                if (data && typeof data === 'object' && !Array.isArray(data) && Array.isArray(data.helpers)) {
                    helpers = data.helpers;
                    seekerBusy = data.seekerBusy === true;
                    busyReason = (data.busyReason != null ? String(data.busyReason) : '') || '';
                } else if (Array.isArray(data)) {
                    helpers = data;
                }
                if (seekerBusy && window.DevExpress && DevExpress.ui && DevExpress.ui.notify) {
                    DevExpress.ui.notify(getBusyCopy(busyReason).notify, 'warning', 5500);
                }
                renderHelpers(helpers, seekerBusy, busyReason);
            });
    }

    function renderHelpers(helpers, seekerBusy, busyReason) {
        const section = document.getElementById('resultsSection');
        const grid = document.getElementById('helpersGrid');
        const count = document.getElementById('helperCount');
        if (!section || !grid || !count) return;

        section.style.display = 'block';
        if (seekerBusy) {
            count.textContent = getBusyCopy(busyReason).badge;
        } else {
            count.textContent = helpers.length === 0 ? 'No user available' : helpers.length + ' found';
        }

        if (!helpers.length) {
            var body = seekerBusy
                ? getBusyCopy(busyReason).body
                : '<p><strong>No user available.</strong><br>No helpers are available for this skill at the selected time. Try a different day, time, or sub-skill.</p>';
            grid.innerHTML = `
                <div class="no-helpers">
                    <div class="icon">😕</div>
                    ${body}
                </div>`;
            return;
        }

        const currentPendingHelperId = window._lastSentHelperId || 0;
        grid.innerHTML = helpers.map((h, i) => {
            const initial = (h.username || '?')[0].toUpperCase();
            const hes = (h.hes != null ? h.hes : h.HES) != null ? Number(h.hes || h.HES).toFixed(4) : '—';
            const safeName = (h.username || 'Unknown').replace(/'/g, "\\'");
            const isMine = isRequestPending && currentPendingHelperId && (Number(h.userId) === Number(currentPendingHelperId));
            return `
            <div class="helper-card ${i === 0 ? 'top-pick' : ''}">
                <div class="helper-rank">#${i + 1} Helper</div>
                <div class="helper-name">
                    <div class="helper-avatar">${initial}</div>
                    ${h.username || 'Unknown'}
                </div>
                <div class="helper-skill-info">
                    <span class="badge-level level-4">Score ${hes}</span>
                </div>
                <div style="display:flex; gap:10px; align-items:center;">
                    <button class="request-btn" data-helper-id="${h.userId}" data-helper-name="${safeName}" ${isRequestPending ? 'disabled' : ''} style="flex:1;">
                        ${isMine ? 'Request Sent' : 'Request Help'}
                    </button>
                    ${isMine ? `<button class="cancel-btn btn btn-sm btn-outline-danger" data-cancel="1" title="Cancel this request">Cancel</button>` : ``}
                </div>
            </div>`;
        }).join('');

        grid.querySelectorAll('.request-btn').forEach(btn => {
            btn.addEventListener('click', function () {
                const helperId = this.getAttribute('data-helper-id');
                const helperName = this.getAttribute('data-helper-name');
                sendRequest(parseInt(helperId, 10), helperName, this);
            });
        });

        grid.querySelectorAll('.cancel-btn[data-cancel="1"]').forEach(function (b) {
            b.addEventListener('click', function () {
                var reqId = window._lastHelpRequestId || 0;
                if (!reqId) return;
                withdraw(reqId, false).then(function (w) {
                    if (w && w.success) {
                        window._lastHelpRequestId = 0;
                        window._lastSentHelperId = 0;
                        requestAttemptedHelpers = [];
                        isRequestPending = false;
                        if (window._requestTimeout) clearTimeout(window._requestTimeout);
                        setAllRequestButtonsEnabled(true);
                        // Re-render current list so the cancel button disappears.
                        renderHelpers(helpers, false, '');
                    }
                });
            });
        });
    }

    function sendRequest(helperId, helperName, btn) {
        if (isRequestPending) return;
        btn.disabled = true;
        btn.textContent = 'Sending...';

        const desc = (document.getElementById('txtProblemDesc')?.value) || '';
        const s = lastSearch;
        const body = new URLSearchParams({
            helperId, fieldId: s.fieldId, skillId: s.skillId, subSkillId: s.subSkillId,
            timeStart: s.timeStart || '', timeEnd: s.timeEnd || '', availableDay: s.day || '', description: desc
        });

        fetch('/User/SendHelpRequest', {
            method: 'POST',
            headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
            body: body.toString()
        })
            .then(r => r.json())
            .then(function (data) {
                if (!data || !data.success) {
                    btn.disabled = false;
                    btn.textContent = 'Request Help';
                    DevExpress.ui.notify((data && data.message) || 'Could not send request.', 'error', 3000);
                    return;
                }

                document.getElementById('requestedHelperName').textContent = helperName;
                document.getElementById('requestOverlay').classList.add('show');
                btn.textContent = 'Request Sent';
                window._lastSentHelperId = helperId;
                window._lastHelpRequestId = data.helpRequestId || 0;
                requestAttemptedHelpers = [helperId];
                isRequestPending = true;
                setAllRequestButtonsEnabled(false);

                if (window._requestTimeout) clearTimeout(window._requestTimeout);
                window._requestTimeout = setTimeout(function () {
                    // If helper hasn't responded within 2 minutes, withdraw to free the seeker.
                    expirePendingRequestAfterTimeout();
                }, 122000);
            });
    }

    function expirePendingRequestAfterTimeout() {
        var reqId = window._lastHelpRequestId || 0;
        if (!reqId) return;

        // If helper already accepted/rejected, withdraw will fail => leave state as-is.
        withdraw(reqId, true).then(function (w) {
            if (!w || !w.success) return;

            window._lastHelpRequestId = 0;
            window._lastSentHelperId = 0;
            requestAttemptedHelpers = [];
            isRequestPending = false;

            // Free the seeker UI so they can choose ANY helper again (same or different).
            setAllRequestButtonsEnabled(true);

            document.getElementById('nextHelperArea').style.display = 'none';
            document.getElementById('requestOverlay').classList.remove('show');

            // Refresh ranking immediately so the lowered score is visible without manual refresh.
            if (lastSearch && lastSearch.fieldId && lastSearch.skillId && lastSearch.subSkillId) {
                findHelpers(
                    lastSearch.fieldId,
                    lastSearch.skillId,
                    lastSearch.subSkillId,
                    lastSearch.timeStart || '',
                    lastSearch.timeEnd || '',
                    lastSearch.day || ''
                );
            }

            if (window.DevExpress && DevExpress.ui && DevExpress.ui.notify) {
                DevExpress.ui.notify('No response in 2 minutes. Your request was cancelled — you can send a new request now.', 'info', 4500);
            }
        });
    }

    function withdraw(helpRequestId, isTimeout) {
        return fetch('/User/WithdrawRequest', {
            method: 'POST',
            headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
            body: 'helpRequestId=' + helpRequestId + '&isTimeout=' + (isTimeout ? 'true' : 'false')
        }).then(r => r.json());
    }

    function init() {
        // Field
        $('#selectField').dxSelectBox({
            dataSource: '/Account/GetFields',
            displayExpr: 'FieldName',
            valueExpr: 'FieldId',
            placeholder: 'Choose field...',
            stylingMode: 'outlined',
            onValueChanged: function (e) {
                const skillBox = $('#selectSkill').dxSelectBox('instance');
                skillBox.option('dataSource', e.value ? '/Account/GetSkillsByField?fieldId=' + e.value : []);
                skillBox.option('value', null);
                const subBox = $('#selectSubSkill').dxSelectBox('instance');
                subBox.option('dataSource', []);
                subBox.option('value', null);
                selectedSubSkillId = null;
            }
        });

        // Skill
        $('#selectSkill').dxSelectBox({
            displayExpr: 'SkillName',
            valueExpr: 'SkillId',
            placeholder: 'Choose skill...',
            stylingMode: 'outlined',
            onValueChanged: function (e) {
                const subBox = $('#selectSubSkill').dxSelectBox('instance');
                subBox.option('dataSource', e.value ? '/Account/GetSubSkillsBySkill?skillId=' + e.value : []);
                subBox.option('value', null);
                selectedSubSkillId = null;
            }
        });

        // Subskill
        $('#selectSubSkill').dxSelectBox({
            displayExpr: 'SubSkillName',
            valueExpr: 'SubSkillId',
            placeholder: 'Choose sub-skill...',
            stylingMode: 'outlined',
            onValueChanged: function (e) { selectedSubSkillId = e.value; }
        });

        // Day
        $('#selectDay').dxSelectBox({
            dataSource: ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday', 'Sunday'],
            placeholder: 'Choose day...',
            stylingMode: 'outlined'
        });

        // Time
        $('#preferredStart').dxDateBox({ type: 'time', placeholder: 'Start time', pickerType: 'list' });
        $('#preferredEnd').dxDateBox({ type: 'time', placeholder: 'End time', pickerType: 'list' });

        // Search
        $('#btnSearch').dxButton({
            text: 'Find Helpers',
            type: 'default',
            width: '100%',
            stylingMode: 'contained',
            onClick: function () {
                const fId = $('#selectField').dxSelectBox('instance').option('value');
                const sId = $('#selectSkill').dxSelectBox('instance').option('value');
                const subId = selectedSubSkillId;
                const day = $('#selectDay').dxSelectBox('instance')?.option('value') || '';
                const startVal = $('#preferredStart').dxDateBox('instance')?.option('value') || null;
                const endVal = $('#preferredEnd').dxDateBox('instance')?.option('value') || null;
                const timeStart = startVal instanceof Date ? startVal.toTimeString().slice(0, 8) : (startVal || '');
                const timeEnd = endVal instanceof Date ? endVal.toTimeString().slice(0, 8) : (endVal || '');

                if (!subId) {
                    DevExpress.ui.notify('Please select a field, skill, and sub-skill first.', 'warning', 2500);
                    return;
                }

                if (startVal && endVal) {
                    var sm = startVal.getHours() * 60 + startVal.getMinutes();
                    var em = endVal.getHours() * 60 + endVal.getMinutes();
                    if (em <= sm) {
                        DevExpress.ui.notify('End time must be later on the same day.', 'warning', 3000);
                        return;
                    }
                }

                lastSearch = { fieldId: fId, skillId: sId, subSkillId: subId, timeStart, timeEnd, day };
                findHelpers(fId, sId, subId, timeStart, timeEnd, day);
            }
        });

        // Next helper button
        $('#sendToNextBtn').on('click', function () {
            var next = window._nextHelper;
            var reqId = window._lastHelpRequestId;
            if (!next || !reqId) return;
            var desc = document.getElementById('txtProblemDesc')?.value || '';
            var s = lastSearch;

            withdraw(reqId, false).then(function (w) {
                if (!w || !w.success) return;
                var body = new URLSearchParams({
                    helperId: next.userId, fieldId: s.fieldId, skillId: s.skillId, subSkillId: s.subSkillId,
                    timeStart: s.timeStart || '', timeEnd: s.timeEnd || '', availableDay: s.day || '', description: desc
                });
                return fetch('/User/SendHelpRequest', { method: 'POST', headers: { 'Content-Type': 'application/x-www-form-urlencoded' }, body: body.toString() });
            }).then(function (r) { return r && r.json ? r.json() : null; })
                .then(function (data) {
                    if (data && data.success) {
                        document.getElementById('requestedHelperName').textContent = next.username || 'Next helper';
                        document.getElementById('nextHelperArea').style.display = 'none';
                        window._lastHelpRequestId = data.helpRequestId || 0;
                        window._lastSentHelperId = next.userId;
                        requestAttemptedHelpers.push(next.userId);

                        if (window._requestTimeout) clearTimeout(window._requestTimeout);
                        window._requestTimeout = setTimeout(function () {
                            expirePendingRequestAfterTimeout();
                        }, 122000);
                    }
                });
        });

        // Cancel current pending request
        $('#cancelRequestBtn').on('click', function () {
            var reqId = window._lastHelpRequestId;
            if (!reqId) {
                document.getElementById('requestOverlay').classList.remove('show');
                if (window._requestTimeout) clearTimeout(window._requestTimeout);
                return;
            }
            withdraw(reqId, false).then(function (w) {
                if (w && w.success) {
                    window._lastHelpRequestId = 0;
                    window._lastSentHelperId = 0;
                    requestAttemptedHelpers = [];
                    isRequestPending = false;
                    document.getElementById('nextHelperArea').style.display = 'none';
                    document.getElementById('requestOverlay').classList.remove('show');
                    if (window._requestTimeout) clearTimeout(window._requestTimeout);
                    setAllRequestButtonsEnabled(true);
                }
            });
        });

        // Safety: if the DB expires/withdraws a request (or helper rejects) while the seeker page is open,
        // automatically free the seeker UI when there is no active request.
        setInterval(function () {
            if (!isRequestPending) return;
            fetch('/User/GetNotificationSnapshot')
                .then(function (r) { return r.json(); })
                .then(function (snap) {
                    var active = (snap && (snap.myActive ?? snap.myactive ?? 0)) || 0;
                    if (active === 0) {
                        window._lastHelpRequestId = 0;
                        window._lastSentHelperId = 0;
                        requestAttemptedHelpers = [];
                        isRequestPending = false;
                        if (window._requestTimeout) clearTimeout(window._requestTimeout);
                        setAllRequestButtonsEnabled(true);
                    }
                });
        }, 4000);
    }

    $(init);
})();