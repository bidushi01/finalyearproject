(function () {
    if (!window.peerAssistUserId) return;

    var userId = String(window.peerAssistUserId).trim();
    window.peerAssistUserId = userId;
    var storageKey = 'peerassist-notify-' + userId;

    function clampInt(n) {
        var x = parseInt(n, 10);
        return isNaN(x) || x < 0 ? 0 : x;
    }

    function loadState() {
        var raw = localStorage.getItem(storageKey) || '';
        var parts = raw.split('|');
        return {
            helpInbox: clampInt(parts[0]),
            myRequests: clampInt(parts[1]),
            messages: clampInt(parts[2])
        };
    }

    function saveState(state) {
        var raw = (state.helpInbox || 0) + '|' + (state.myRequests || 0) + '|' + (state.messages || 0);
        localStorage.setItem(storageKey, raw);
    }

    var state = loadState();

    function renderBadges() {
        var inboxEl = document.getElementById('navHelpInboxBadge');
        var myReqEl = document.getElementById('navMyRequestsBadge');

        if (inboxEl) {
            if (state.helpInbox > 0) {
                inboxEl.textContent = state.helpInbox;
                inboxEl.classList.remove('d-none');
            } else {
                inboxEl.classList.add('d-none');
            }
        }

        if (myReqEl) {
            if (state.myRequests > 0) {
                myReqEl.textContent = state.myRequests;
                myReqEl.classList.remove('d-none');
            } else {
                myReqEl.classList.add('d-none');
            }
        }
    }

    window.peerAssistNotifications = {
        clearHelpInbox: function () {
            state.helpInbox = 0;
            saveState(state);
            renderBadges();
        },
        clearMyRequests: function () {
            state.myRequests = 0;
            saveState(state);
            renderBadges();
        },
        clearMessages: function () {
            state.messages = 0;
            saveState(state);
            renderBadges();
        },
        incrementMyRequests: function (delta) {
            state.myRequests = Math.max(0, state.myRequests + (delta || 1));
            saveState(state);
            renderBadges();
        }
    };

    if (window.$ && $.getJSON) {
        $.getJSON('/User/GetNotificationSnapshot')
            .done(function (snap) {
                if (!snap) return;
                state.helpInbox = snap.inboxPending ?? state.helpInbox;
                state.myRequests = snap.myActive ?? state.myRequests;
                saveState(state);
                renderBadges();
            })
            .fail(function () {
                renderBadges();
            });
    } else {
        renderBadges();
    }

    if (!window.signalR) {
        console.warn('SignalR library not loaded.');
        return;
    }

    var connectPromise = null;
    var connection = new signalR.HubConnectionBuilder()
        .withUrl('/helpHub', { withCredentials: true })
        .withAutomaticReconnect()
        .build();

    function notifyUser(message, level) {
        if (window.DevExpress && DevExpress.ui && DevExpress.ui.notify) {
            DevExpress.ui.notify(message, level || 'info', 8000);
        }
    }

    connection.on('HelpRequestAccepted', function (helperName, helpRequestId) {
        state.myRequests = Math.max(0, state.myRequests - 1);
        saveState(state);
        renderBadges();
        notifyUser((helperName || 'Your helper') + ' accepted your help request. Open chat to connect.', 'success');
        if (window.peerAssistOnHelpRequestAccepted) {
            window.peerAssistOnHelpRequestAccepted(helpRequestId, helperName);
        }
    });

    connection.on('HelpRequestReceived', function (seekerName) {
        state.helpInbox = state.helpInbox + 1;
        saveState(state);
        renderBadges();
        notifyUser('New help request from ' + seekerName, 'info');
    });

    connection.on('MessageReceived', function (fromName) {
        state.messages = state.messages + 1;
        saveState(state);
        renderBadges();
        notifyUser('New message from ' + fromName, 'info');
    });

    connection.on('HelpRequestWithdrawn', function () {
        state.helpInbox = Math.max(0, state.helpInbox - 1);
        saveState(state);
        renderBadges();
    });

    connection.onreconnected(function () {
        connection.invoke('JoinUserGroup', userId).catch(function () { });
    });

    function ensureHubConnected() {
        if (connection.state === signalR.HubConnectionState.Connected) {
            return Promise.resolve(connection);
        }
        if (connection.state === signalR.HubConnectionState.Connecting) {
            return connectPromise || Promise.reject(new Error('SignalR is still connecting.'));
        }
        if (!connectPromise) {
            connectPromise = connection.start()
                .then(function () {
                    window.peerAssistHubConnection = connection;
                    return connection.invoke('JoinUserGroup', userId);
                })
                .then(function () { return connection; })
                .catch(function (err) {
                    connectPromise = null;
                    window.peerAssistHubConnection = null;
                    console.error('SignalR connection failed:', err);
                    throw err;
                });
        }
        return connectPromise;
    }

    window.peerAssistHub = {
        getConnection: function () { return connection; },
        ensureConnected: ensureHubConnected
    };

    ensureHubConnected().catch(function () {
        console.warn('Real-time notifications will retry when you use chat or video.');
    });

})();
