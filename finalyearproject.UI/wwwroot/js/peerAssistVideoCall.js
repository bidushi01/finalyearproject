(function () {
    if (!window.peerAssistUserId || !window.signalR) return;

    var iceServers = [{ urls: 'stun:stun.l.google.com:19302' }];
    var localStream = null;
    var screenTrack = null;
    var blackTrack = null;
    var remoteStream = null;
    var pc = null;
    var videoSender = null;
    var activeHelpRequestId = 0;
    var activeStatus = '';
    var activeOtherName = '';
    var partiesLoaded = false;
    var isStartingCall = false;
    var makingOffer = false;
    var isPolite = false;
    var callAccepted = false;
    var isCaller = false;
    var pendingInvite = null;
    var ringTimeout = null;
    var screenSharing = false;
    var callExpanded = false;
    var cameraOn = false;
    var micMuted = false;

    function storeKey() { return 'peerassist-call-' + window.peerAssistUserId; }
    function $(id) { return document.getElementById(id); }

    function notify(message, level) {
        if (window.DevExpress && DevExpress.ui && DevExpress.ui.notify) {
            var ms = (level === 'info' && message.indexOf('Tip:') === 0) ? 12000 : 6000;
            DevExpress.ui.notify(message, level || 'warning', ms);
        }
    }

    function setStatus(text) {
        var el = $('peerAssistVideoStatus');
        if (el) {
            el.textContent = text || '';
            el.classList.toggle('d-none', !text);
        }
    }

    function saveCallState() {
        if (!callAccepted || !activeHelpRequestId) {
            sessionStorage.removeItem(storeKey());
            return;
        }
        var meta = $('peerAssistVideoMeta');
        sessionStorage.setItem(storeKey(), JSON.stringify({
            helpRequestId: activeHelpRequestId,
            otherName: activeOtherName,
            seekerId: meta ? (meta.getAttribute('data-seeker-id') || '') : '',
            helperId: meta ? (meta.getAttribute('data-helper-id') || '') : '',
            expanded: callExpanded,
            micMuted: micMuted
        }));
    }

    function clearCallState() {
        sessionStorage.removeItem(storeKey());
    }

    function setCallPeerTitle(name) {
        var el = $('peerAssistCallPeerName');
        if (el) el.textContent = name ? ('Call with ' + name) : 'In call';
    }

    function setCallExpanded(expanded) {
        callExpanded = !!expanded;
        var panel = $('peerAssistActiveCallPanel');
        var expandBtn = $('peerAssistExpandCallBtn');
        var minBtn = $('peerAssistMinimizeCallBtn');
        if (!panel) return;
        panel.classList.toggle('peer-call-expanded', callExpanded);
        panel.classList.toggle('peer-call-minimized', !callExpanded);
        if (expandBtn) expandBtn.classList.toggle('d-none', callExpanded);
        if (minBtn) minBtn.classList.toggle('d-none', !callExpanded);
        saveCallState();
    }

    function showStartButton(show) {
        document.querySelectorAll('#peerAssistVideoActions').forEach(function (el) {
            el.classList.toggle('d-none', !show);
        });
    }

    function setStartButtonEnabled(enabled) {
        document.querySelectorAll('#peerAssistStartVideoBtn').forEach(function (btn) {
            btn.disabled = !enabled;
        });
    }

    function showActivePanel(show) {
        var panel = $('peerAssistActiveCallPanel');
        if (!panel) return;
        panel.classList.toggle('peer-call-open', !!show);
        panel.style.display = show ? 'flex' : 'none';
        if (show) {
            setCallPeerTitle(activeOtherName);
            if (!callExpanded) setCallExpanded(false);
            else setCallExpanded(true);
        }
        showStartButton(!show && activeStatus === 'accepted' && !callAccepted);
        if (show) saveCallState();
    }

    function showIncomingOverlay(show) {
        var el = $('peerAssistIncomingCall');
        if (!el) return;
        el.classList.toggle('peer-call-open', !!show);
        el.style.display = show ? 'flex' : 'none';
    }

    function showOutgoingOverlay(show) {
        var el = $('peerAssistOutgoingCall');
        if (!el) return;
        el.classList.toggle('peer-call-open', !!show);
        el.style.display = show ? 'flex' : 'none';
    }

    function setLocalCameraOff() {
        cameraOn = false;
        var wrap = $('peerAssistLocalVideoWrap');
        var local = $('peerAssistLocalVideo');
        if (local) local.srcObject = null;
        if (wrap) wrap.classList.add('camera-off');
        syncCameraButton();
    }

    function setLocalCameraOn() {
        cameraOn = true;
        var wrap = $('peerAssistLocalVideoWrap');
        var local = $('peerAssistLocalVideo');
        if (local && localStream) local.srcObject = localStream;
        if (wrap) wrap.classList.remove('camera-off');
        syncCameraButton();
    }

    function setRemoteCameraOff(off) {
        var wrap = $('peerAssistRemoteVideoWrap');
        if (wrap) wrap.classList.toggle('camera-off', !!off);
    }

    function waitForSignalingStable(maxMs) {
        maxMs = maxMs || 5000;
        return new Promise(function (resolve) {
            if (!pc || pc.signalingState === 'stable') {
                resolve(true);
                return;
            }
            var elapsed = 0;
            var timer = setInterval(function () {
                elapsed += 50;
                if (!pc || pc.signalingState === 'stable' || elapsed >= maxMs) {
                    clearInterval(timer);
                    resolve(pc && pc.signalingState === 'stable');
                }
            }, 50);
        });
    }

    function getBlackVideoTrack() {
        if (blackTrack && blackTrack.readyState === 'live') return blackTrack;
        var canvas = document.createElement('canvas');
        canvas.width = 640;
        canvas.height = 480;
        var ctx = canvas.getContext('2d');
        ctx.fillStyle = '#000000';
        ctx.fillRect(0, 0, canvas.width, canvas.height);
        blackTrack = canvas.captureStream(5).getVideoTracks()[0];
        return blackTrack;
    }

    function syncMuteButton() {
        var btn = $('peerAssistMuteBtn');
        if (btn) btn.textContent = micMuted ? 'Unmute' : 'Mute';
    }

    function applyMicState() {
        if (!localStream) return;
        var audio = localStream.getAudioTracks()[0];
        if (audio) audio.enabled = !micMuted;
        syncMuteButton();
    }

    function clearRingTimeout() {
        if (ringTimeout) {
            clearTimeout(ringTimeout);
            ringTimeout = null;
        }
    }

    async function ensureHub() {
        if (window.peerAssistHub && window.peerAssistHub.ensureConnected) {
            return window.peerAssistHub.ensureConnected();
        }
        var conn = window.peerAssistHubConnection;
        if (conn && conn.state === signalR.HubConnectionState.Connected) return conn;
        throw new Error('Real-time connection is not ready. Refresh the page and try again.');
    }

    function registerHandlers(conn) {
        if (conn.__peerAssistVideoHandlers) return;
        conn.__peerAssistVideoHandlers = true;

        conn.on('VideoCallInvite', function (a, b, c) { onVideoCallInvite(a, b, c); });
        conn.on('VideoCallAccepted', function (a, b) { onVideoCallAccepted(a, b); });
        conn.on('VideoCallDeclined', function (a, b) { onVideoCallDeclined(a, b); });
        conn.on('VideoCallCancelled', function (a, b) { onVideoCallCancelled(a, b); });
        conn.on('CallPeerLeft', function (peerUserId) {
            if (String(peerUserId) === String(window.peerAssistUserId)) return;
            notify('Peer left the call.', 'info');
            endCallLocal(true);
        });
        conn.on('PeerCallReconnecting', function (peerUserId, helpRequestId) {
            if (helpRequestId !== activeHelpRequestId || !callAccepted || !pc) return;
            if (String(peerUserId) === String(window.peerAssistUserId)) return;
            renegotiate().catch(function () { });
        });
        conn.on('ReceiveVideoOffer', function (fromUserId, helpRequestId, sdp) {
            if (helpRequestId !== activeHelpRequestId || !callAccepted) return;
            if (String(fromUserId) === String(window.peerAssistUserId)) return;
            handleOffer(fromUserId, sdp);
        });
        conn.on('ReceiveVideoAnswer', function (fromUserId, helpRequestId, sdp) {
            if (helpRequestId !== activeHelpRequestId || !pc) return;
            pc.setRemoteDescription({ type: 'answer', sdp: sdp }).then(function () {
                refreshRemoteMedia();
                setStatus('');
            }).catch(function () { });
        });
        conn.on('ReceiveIceCandidate', function (fromUserId, helpRequestId, candidate, sdpMid, sdpMLineIndex) {
            if (helpRequestId !== activeHelpRequestId || !pc || !candidate) return;
            pc.addIceCandidate({
                candidate: candidate,
                sdpMid: sdpMid || null,
                sdpMLineIndex: sdpMLineIndex
            }).catch(function () { });
        });
    }

    function mediaErrorMessage(err) {
        var name = err && err.name ? err.name : '';
        if (name === 'NotAllowedError' || name === 'PermissionDeniedError') {
            return 'Microphone/camera blocked. Allow access in the browser address bar.';
        }
        if (name === 'NotFoundError' || name === 'DevicesNotFoundError') {
            return 'No microphone found.';
        }
        if (name === 'NotReadableError' || name === 'TrackStartError') {
            return 'Camera or mic is busy. Close other apps using the camera.';
        }
        return 'Could not access media: ' + (err && err.message ? err.message : 'unknown error');
    }

    function stopCameraVideoTracks() {
        if (!localStream) return;
        localStream.getVideoTracks().forEach(function (t) {
            if (t !== blackTrack && t !== screenTrack) {
                t.stop();
                localStream.removeTrack(t);
            }
        });
    }

    function stopRealVideoTracks() {
        stopCameraVideoTracks();
        if (screenTrack) {
            screenTrack.stop();
            screenTrack = null;
            screenSharing = false;
        }
    }

    async function getLocalAudio() {
        if (localStream && localStream.getAudioTracks().length) {
            applyMicState();
            return localStream;
        }
        if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) {
            throw new Error('This browser does not support calls. Use Chrome or Edge.');
        }
        var audio = await navigator.mediaDevices.getUserMedia({
            video: false,
            audio: { echoCancellation: true, noiseSuppression: true, autoGainControl: true }
        });
        if (!localStream) {
            localStream = audio;
        } else {
            audio.getAudioTracks().forEach(function (t) { localStream.addTrack(t); });
        }
        applyMicState();
        setLocalCameraOff();
        syncShareButton();
        return localStream;
    }

    function getVideoSender() {
        if (!pc) return null;
        if (videoSender && pc.getSenders().indexOf(videoSender) >= 0) return videoSender;
        videoSender = pc.getSenders().find(function (s) { return s.track && s.track.kind === 'video'; }) || null;
        if (!videoSender) {
            var tr = pc.addTransceiver('video', { direction: 'sendrecv' });
            videoSender = tr.sender;
        }
        return videoSender;
    }

    async function sendVideoToPeer(track) {
        if (!pc) return;
        var sender = getVideoSender();
        if (sender) await sender.replaceTrack(track);
        await waitForSignalingStable();
        await renegotiate();
        saveCallState();
    }

    async function addCameraTrack() {
        if (screenSharing && screenTrack) {
            screenTrack.stop();
            screenTrack = null;
            screenSharing = false;
        }
        stopRealVideoTracks();
        var vid = await navigator.mediaDevices.getUserMedia({
            video: { width: { ideal: 640 }, height: { ideal: 480 }, facingMode: 'user' },
            audio: false
        });
        var track = vid.getVideoTracks()[0];
        if (!track) throw new Error('No camera found.');
        if (!localStream) localStream = new MediaStream();
        localStream.addTrack(track);
        setLocalCameraOn();
        await sendVideoToPeer(track);
    }

    async function renegotiate() {
        var otherId = getOtherPartyUserId();
        if (!otherId || !activeHelpRequestId || !pc) return;
        if (makingOffer) return;
        if (!(await waitForSignalingStable())) return;
        makingOffer = true;
        try {
            var offer = await pc.createOffer();
            await pc.setLocalDescription(offer);
            var conn = await ensureHub();
            await conn.invoke('SendVideoOffer', activeHelpRequestId, String(otherId), offer.sdp);
        } catch (e) {
            notify('Could not update video. Try toggling camera again.', 'warning');
        } finally {
            makingOffer = false;
        }
    }

    function getRemoteVolume() {
        var slider = $('peerAssistRemoteVolume');
        if (!slider) return 1;
        return Math.max(0, Math.min(1, parseInt(slider.value, 10) / 100));
    }

    function bindRemoteVideoTrack(track) {
        if (!track || track.kind !== 'video' || track.__peerAssistBound) return;
        track.__peerAssistBound = true;
        track.onunmute = function () {
            setRemoteCameraOff(false);
            refreshRemoteMedia();
        };
        track.onended = function () {
            refreshRemoteMedia();
        };
    }

    function refreshRemoteMedia() {
        if (!pc) return;
        if (!remoteStream) remoteStream = new MediaStream();

        var videoTrack = null;
        var audioTrack = null;
        pc.getReceivers().forEach(function (receiver) {
            if (!receiver.track || receiver.track.readyState !== 'live') return;
            if (receiver.track.kind === 'video') videoTrack = receiver.track;
            if (receiver.track.kind === 'audio') audioTrack = receiver.track;
        });

        remoteStream.getVideoTracks().slice().forEach(function (t) { remoteStream.removeTrack(t); });
        remoteStream.getAudioTracks().slice().forEach(function (t) { remoteStream.removeTrack(t); });
        if (videoTrack) {
            remoteStream.addTrack(videoTrack);
            bindRemoteVideoTrack(videoTrack);
        }
        if (audioTrack) remoteStream.addTrack(audioTrack);

        attachRemoteStream(remoteStream);
    }

    function attachRemoteStream(stream) {
        var remoteVideo = $('peerAssistRemoteVideo');
        var remoteAudio = $('peerAssistRemoteAudio');
        var vol = getRemoteVolume();
        var vt = stream && stream.getVideoTracks()[0];

        if (remoteVideo) {
            remoteVideo.srcObject = null;
            remoteVideo.srcObject = stream;
            remoteVideo.volume = vol;
            remoteVideo.play().catch(function () { });
        }
        if (remoteAudio) {
            remoteAudio.srcObject = null;
            remoteAudio.srcObject = stream;
            remoteAudio.volume = vol;
            remoteAudio.play().catch(function () { });
        }

        if (vt && vt.readyState === 'live') {
            setRemoteCameraOff(false);
        } else {
            setRemoteCameraOff(true);
        }
    }

    async function ensurePeerConnection(targetUserId) {
        if (pc) return pc;
        videoSender = null;
        pc = new RTCPeerConnection({ iceServers: iceServers });

        pc.onicecandidate = function (e) {
            if (!e.candidate || !activeHelpRequestId || !targetUserId) return;
            var conn = window.peerAssistHubConnection;
            if (!conn || conn.state !== signalR.HubConnectionState.Connected) return;
            conn.invoke('SendIceCandidate',
                activeHelpRequestId,
                String(targetUserId),
                e.candidate.candidate,
                e.candidate.sdpMid,
                e.candidate.sdpMLineIndex).catch(function () { });
        };

        pc.ontrack = function () {
            refreshRemoteMedia();
        };

        pc.onconnectionstatechange = function () {
            if (pc && pc.connectionState === 'connected') {
                refreshRemoteMedia();
            }
            if (pc && pc.connectionState === 'failed') {
                setStatus('Connection failed — tap End and call again.');
            }
        };

        if (localStream) {
            localStream.getAudioTracks().forEach(function (t) { pc.addTrack(t, localStream); });
        }
        pc.addTransceiver('video', { direction: 'sendrecv' });
        videoSender = pc.getTransceivers().slice(-1)[0].sender;
        if (videoSender) {
            await videoSender.replaceTrack(getBlackVideoTrack());
        }

        return pc;
    }

    function getOtherPartyUserId() {
        var el = $('peerAssistVideoMeta');
        if (!el) return null;
        var seeker = (el.getAttribute('data-seeker-id') || '').trim();
        var helper = (el.getAttribute('data-helper-id') || '').trim();
        var me = String(window.peerAssistUserId).trim();
        if (me === seeker) return helper || null;
        if (me === helper) return seeker || null;
        return null;
    }

    function applyPartyMeta(seekerId, helperId) {
        var meta = $('peerAssistVideoMeta');
        if (meta) {
            if (seekerId) meta.setAttribute('data-seeker-id', seekerId);
            if (helperId) meta.setAttribute('data-helper-id', helperId);
        }
    }

    function loadParties(helpRequestId) {
        var hrId = helpRequestId || activeHelpRequestId;
        partiesLoaded = false;
        setStartButtonEnabled(false);
        if (!hrId) return Promise.resolve(false);
        return fetch('/User/GetVideoCallParties?helpRequestId=' + hrId)
            .then(function (r) { return r.json(); })
            .then(function (d) {
                if (d && d.success) {
                    applyPartyMeta(d.seekerId, d.helperId);
                    partiesLoaded = true;
                    setStartButtonEnabled(activeStatus === 'accepted' && !callAccepted);
                    return true;
                }
                return false;
            })
            .catch(function () { return false; });
    }

    async function beginWebRtcAsCaller() {
        showOutgoingOverlay(false);
        showActivePanel(true);
        setStatus('Connecting…');
        await getLocalAudio();
        var otherId = getOtherPartyUserId();
        if (!otherId) throw new Error('Could not load session partner.');
        var conn = await ensureHub();
        await conn.invoke('JoinVideoCall', activeHelpRequestId);
        await ensurePeerConnection(otherId);
        makingOffer = true;
        try {
            var offer = await pc.createOffer();
            await pc.setLocalDescription(offer);
            await conn.invoke('SendVideoOffer', activeHelpRequestId, String(otherId), offer.sdp);
            setStatus('');
            saveCallState();
        } finally {
            makingOffer = false;
        }
    }

    async function beginWebRtcAsCallee() {
        showIncomingOverlay(false);
        showActivePanel(true);
        callAccepted = true;
        isCaller = false;
        setStatus('Connecting…');
        await getLocalAudio();
        var conn = await ensureHub();
        await conn.invoke('JoinVideoCall', activeHelpRequestId);
        setStatus('');
        saveCallState();
    }

    async function handleOffer(fromUserId, sdp) {
        var otherId = String(fromUserId);
        isPolite = parseInt(window.peerAssistUserId, 10) > parseInt(otherId, 10);
        var offerCollision = makingOffer || (pc && pc.signalingState !== 'stable');
        if (offerCollision && !isPolite) return;
        if (!callAccepted) return;
        try {
            await getLocalAudio();
            await ensurePeerConnection(otherId);
            if (offerCollision && isPolite) {
                await pc.setLocalDescription({ type: 'rollback' });
            }
            await pc.setRemoteDescription({ type: 'offer', sdp: sdp });
            var answer = await pc.createAnswer();
            await pc.setLocalDescription(answer);
            var conn = await ensureHub();
            await conn.invoke('SendVideoAnswer', activeHelpRequestId, otherId, answer.sdp);
            refreshRemoteMedia();
            setStatus('');
            saveCallState();
        } catch (e) {
            setStatus('Reconnecting video…');
        } finally {
            makingOffer = false;
        }
    }

    async function reconnectActiveCall(saved) {
        activeHelpRequestId = saved.helpRequestId;
        activeStatus = 'accepted';
        activeOtherName = saved.otherName || '';
        callAccepted = true;
        callExpanded = false;
        micMuted = saved.micMuted === true;
        applyPartyMeta(saved.seekerId, saved.helperId);
        partiesLoaded = true;
        setCallPeerTitle(activeOtherName);
        showActivePanel(true);
        setStatus('Reconnecting…');
        try {
            pc = null;
            videoSender = null;
            remoteStream = null;
            await getLocalAudio();
            var otherId = getOtherPartyUserId();
            if (!otherId) await loadParties(activeHelpRequestId);
            otherId = getOtherPartyUserId();
            if (!otherId) throw new Error('No partner');
            var conn = await ensureHub();
            registerHandlers(conn);
            await conn.invoke('JoinVideoCall', activeHelpRequestId);
            await ensurePeerConnection(otherId);
            isPolite = parseInt(window.peerAssistUserId, 10) > parseInt(otherId, 10);
            if (isPolite) await renegotiate();
            setStatus('');
            saveCallState();
        } catch (e) {
            clearCallState();
            endCallLocal(false);
        }
    }

    async function restoreCallState() {
        var raw = sessionStorage.getItem(storeKey());
        if (!raw) return;
        try {
            var saved = JSON.parse(raw);
            if (!saved || !saved.helpRequestId) return;
            await reconnectActiveCall(saved);
        } catch (e) {
            clearCallState();
        }
    }

    function onVideoCallInvite(fromUserId, helpRequestId, callerName) {
        if (String(fromUserId) === String(window.peerAssistUserId)) return;
        if (pendingInvite || callAccepted || isCaller) return;
        pendingInvite = {
            fromUserId: String(fromUserId),
            helpRequestId: parseInt(helpRequestId, 10),
            callerName: callerName || 'Someone'
        };
        var nameEl = $('peerAssistIncomingCallerName');
        if (nameEl) nameEl.textContent = pendingInvite.callerName;
        showIncomingOverlay(true);
        notify(pendingInvite.callerName + ' is calling — tap Accept or Decline.', 'info');
        clearRingTimeout();
        ringTimeout = setTimeout(function () {
            if (pendingInvite && pendingInvite.helpRequestId === helpRequestId) {
                declineIncomingCall(true);
            }
        }, 45000);
    }

    async function acceptIncomingCall() {
        if (!pendingInvite) return;
        clearRingTimeout();
        var inv = pendingInvite;
        pendingInvite = null;
        activeHelpRequestId = inv.helpRequestId;
        activeStatus = 'accepted';
        activeOtherName = inv.callerName;
        callAccepted = true;
        isCaller = false;
        await loadParties(activeHelpRequestId);
        showIncomingOverlay(false);
        try {
            var conn = await ensureHub();
            await conn.invoke('RespondVideoCallInvite', activeHelpRequestId, inv.fromUserId, true);
            await beginWebRtcAsCallee();
        } catch (e) {
            notify(mediaErrorMessage(e), 'error');
            endCallLocal(true);
        }
    }

    async function declineIncomingCall(silent) {
        if (!pendingInvite) return;
        clearRingTimeout();
        var inv = pendingInvite;
        pendingInvite = null;
        showIncomingOverlay(false);
        try {
            var conn = await ensureHub();
            await conn.invoke('RespondVideoCallInvite', inv.helpRequestId, inv.fromUserId, false);
        } catch (e) { }
        if (!silent) notify('Call declined.', 'info');
    }

    async function onVideoCallAccepted(fromUserId, helpRequestId) {
        if (!isCaller || helpRequestId !== activeHelpRequestId) return;
        clearRingTimeout();
        callAccepted = true;
        try {
            await beginWebRtcAsCaller();
        } catch (e) {
            notify(mediaErrorMessage(e), 'error');
            cancelOutgoingCall(false);
        }
    }

    function onVideoCallDeclined(fromUserId, helpRequestId) {
        if (!isCaller || helpRequestId !== activeHelpRequestId) return;
        clearRingTimeout();
        showOutgoingOverlay(false);
        isCaller = false;
        callAccepted = false;
        notify('Call declined.', 'warning');
        setStartButtonEnabled(true);
    }

    function onVideoCallCancelled(fromUserId, helpRequestId) {
        if (pendingInvite && pendingInvite.helpRequestId === helpRequestId) {
            pendingInvite = null;
            clearRingTimeout();
            showIncomingOverlay(false);
            notify('Call cancelled.', 'info');
        }
    }

    async function startVideoCall() {
        if (isStartingCall || isCaller || callAccepted) return;
        if (activeStatus !== 'accepted') {
            notify('Video is only available during an active (Accepted) session.', 'warning');
            return;
        }
        if (!window.RTCPeerConnection) {
            notify('Your browser does not support video calls.', 'error');
            return;
        }
        isStartingCall = true;
        setStartButtonEnabled(false);
        try {
            if (!partiesLoaded) await loadParties();
            var otherId = getOtherPartyUserId();
            if (!otherId) {
                notify('Session partner not loaded yet.', 'warning');
                return;
            }
            isCaller = true;
            callAccepted = false;
            var calleeEl = $('peerAssistOutgoingCalleeName');
            if (calleeEl) calleeEl.textContent = activeOtherName || 'Peer';
            showOutgoingOverlay(true);
            var conn = await ensureHub();
            registerHandlers(conn);
            var me = await fetch('/User/GetUserInfo').then(function (r) { return r.json(); }).catch(function () { return null; });
            var callerName = (me && me.username) ? me.username : 'Someone';
            await conn.invoke('SendVideoCallInvite', activeHelpRequestId, String(otherId), callerName);
            clearRingTimeout();
            ringTimeout = setTimeout(function () {
                if (isCaller && !callAccepted) cancelOutgoingCall(true);
            }, 45000);
        } catch (e) {
            isCaller = false;
            showOutgoingOverlay(false);
            notify(e.message || 'Could not start call.', 'error');
            setStartButtonEnabled(true);
        } finally {
            isStartingCall = false;
        }
    }

    async function cancelOutgoingCall(noAnswer) {
        clearRingTimeout();
        var otherId = getOtherPartyUserId();
        showOutgoingOverlay(false);
        isCaller = false;
        callAccepted = false;
        if (otherId && activeHelpRequestId) {
            try {
                var conn = await ensureHub();
                await conn.invoke('CancelVideoCallInvite', activeHelpRequestId, String(otherId));
            } catch (e) { }
        }
        setStartButtonEnabled(true);
        if (noAnswer) notify('No answer.', 'warning');
    }

    async function endCallLocal(notifyServer) {
        clearRingTimeout();
        pendingInvite = null;
        isCaller = false;
        callAccepted = false;
        cameraOn = false;
        callExpanded = false;
        clearCallState();
        showIncomingOverlay(false);
        showOutgoingOverlay(false);
        showActivePanel(false);

        if (screenTrack) { screenTrack.stop(); screenTrack = null; }
        if (blackTrack) { blackTrack.stop(); blackTrack = null; }
        screenSharing = false;

        var hrId = activeHelpRequestId;
        if (notifyServer && hrId) {
            try {
                var conn = window.peerAssistHubConnection;
                if (conn && conn.state === signalR.HubConnectionState.Connected) {
                    await conn.invoke('LeaveVideoCall', hrId);
                }
            } catch (e) { }
        }

        if (pc) { pc.close(); pc = null; }
        videoSender = null;
        remoteStream = null;
        if (localStream) {
            localStream.getTracks().forEach(function (t) { t.stop(); });
            localStream = null;
        }

        ['peerAssistLocalVideo', 'peerAssistRemoteVideo', 'peerAssistRemoteAudio'].forEach(function (id) {
            var el = $(id);
            if (el) el.srcObject = null;
        });
        setLocalCameraOff();
        setRemoteCameraOff(true);
        syncShareButton();
        setStartButtonEnabled(activeStatus === 'accepted' && partiesLoaded);
    }

    function syncCameraButton() {
        var btn = $('peerAssistCameraBtn');
        if (!btn) return;
        btn.textContent = cameraOn ? 'Camera off' : 'Camera on';
    }

    function syncShareButton() {
        var btn = $('peerAssistShareBtn');
        if (btn) btn.textContent = screenSharing ? 'Stop share' : 'Share screen';
    }

    function toggleMute() {
        if (!localStream) return;
        micMuted = !micMuted;
        applyMicState();
        saveCallState();
    }

    async function toggleCamera() {
        if (cameraOn || screenSharing) {
            stopRealVideoTracks();
            setLocalCameraOff();
            await sendVideoToPeer(getBlackVideoTrack());
            return;
        }
        try {
            await addCameraTrack();
        } catch (e) {
            notify(mediaErrorMessage(e), 'error');
        }
    }

    async function toggleScreenShare() {
        if (screenSharing && screenTrack) {
            screenTrack.stop();
            screenTrack = null;
            screenSharing = false;
            setLocalCameraOff();
            await sendVideoToPeer(getBlackVideoTrack());
            syncShareButton();
            return;
        }
        try {
            notify('Tip: In the next dialog, choose Window or Tab — not Entire screen — to avoid the mirror effect. Share an app like VS Code or a document.', 'info');
            stopCameraVideoTracks();
            var display = await navigator.mediaDevices.getDisplayMedia({
                video: true,
                audio: false
            });
            var track = display.getVideoTracks()[0];
            if (!track) return;
            screenTrack = track;
            screenSharing = true;
            if (!localStream) localStream = new MediaStream();
            localStream.addTrack(track);
            setLocalCameraOn();
            track.onended = function () {
                screenSharing = false;
                screenTrack = null;
                setLocalCameraOff();
                sendVideoToPeer(getBlackVideoTrack()).catch(function () { });
                syncShareButton();
            };
            await sendVideoToPeer(track);
            syncShareButton();
            notify('Screen sharing is on. Your peer can see what you selected.', 'success');
        } catch (e) {
            if (e && e.name !== 'NotAllowedError') notify('Screen share failed.', 'error');
        }
    }

    function bindButtons() {
        if (document.__peerAssistCallButtonsBound) return;
        document.__peerAssistCallButtonsBound = true;

        document.addEventListener('click', function (e) {
            var t = e.target;
            if (!t || !t.id) return;
            if (t.id === 'peerAssistStartVideoBtn') { startVideoCall(); return; }
            if (t.id === 'peerAssistEndVideoBtn') { endCallLocal(true); return; }
            if (t.id === 'peerAssistMuteBtn') { toggleMute(); return; }
            if (t.id === 'peerAssistCameraBtn') { toggleCamera(); return; }
            if (t.id === 'peerAssistShareBtn') { toggleScreenShare(); return; }
            if (t.id === 'peerAssistAcceptCallBtn') { acceptIncomingCall(); return; }
            if (t.id === 'peerAssistDeclineCallBtn') { declineIncomingCall(false); return; }
            if (t.id === 'peerAssistCancelCallBtn') { cancelOutgoingCall(false); return; }
            if (t.id === 'peerAssistExpandCallBtn') { setCallExpanded(true); return; }
            if (t.id === 'peerAssistMinimizeCallBtn') { setCallExpanded(false); return; }
        });

        document.addEventListener('input', function (e) {
            if (e.target && e.target.id === 'peerAssistRemoteVolume') {
                var v = getRemoteVolume();
                var rv = $('peerAssistRemoteVideo');
                var ra = $('peerAssistRemoteAudio');
                if (rv) rv.volume = v;
                if (ra) ra.volume = v;
            }
        });

        window.addEventListener('beforeunload', function () {
            if (callAccepted) saveCallState();
        });
    }

    window.peerAssistVideoCall = {
        onChatOpened: function (opts) {
            opts = opts || {};
            if (opts.helpRequestId) activeHelpRequestId = opts.helpRequestId;
            if (opts.status) activeStatus = (opts.status || '').toLowerCase();
            if (opts.otherName) activeOtherName = opts.otherName;
            bindButtons();
            if (callAccepted && activeHelpRequestId) {
                showActivePanel(true);
                applyMicState();
                if (opts.helpRequestId === activeHelpRequestId) loadParties(activeHelpRequestId);
            } else {
                showStartButton(activeStatus === 'accepted');
                if (activeStatus === 'accepted') {
                    partiesLoaded = false;
                    loadParties(activeHelpRequestId);
                } else {
                    setStartButtonEnabled(false);
                }
            }
            ensureHub().then(function (c) { registerHandlers(c); }).catch(function () { });
        },
        onChatClosed: function () {
            if (isCaller && !callAccepted) cancelOutgoingCall(false);
        },
        startFromChat: startVideoCall,
        isCallActive: function () { return callAccepted; }
    };

    bindButtons();
    ensureHub().then(function (c) {
        registerHandlers(c);
        return restoreCallState();
    }).catch(function () { });
})();
