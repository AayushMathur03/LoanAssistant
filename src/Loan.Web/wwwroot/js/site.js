// Modern Vanilla JS Helpers for Loan Assistant UI

document.addEventListener('DOMContentLoaded', () => {
    initPersonaSwitching();
    initFieldConfirmations();
    initCitationCopying();
});

function initPersonaSwitching() {
    const buttons = document.querySelectorAll('.persona-btn');
    buttons.forEach(btn => {
        btn.addEventListener('click', (e) => {
            buttons.forEach(b => b.classList.remove('active'));
            const targetBtn = e.target;
            targetBtn.classList.add('active');
            
            const persona = targetBtn.dataset.persona;
            if (persona === 'applicant') {
                window.location.href = '/Applicant';
            } else if (persona === 'officer') {
                window.location.href = '/Officer';
            } else if (persona === 'compliance') {
                window.location.href = '/Compliance';
            } else if (persona === 'admin') {
                window.location.href = '/Admin';
            }
        });
    });
}

function initFieldConfirmations() {
    const confirmButtons = document.querySelectorAll('.btn-confirm-field');
    confirmButtons.forEach(btn => {
        btn.addEventListener('click', (e) => {
            const row = e.target.closest('tr');
            if (row) {
                const badge = row.querySelector('.badge');
                if (badge) {
                    badge.className = 'badge badge-success';
                    badge.textContent = 'Confirmed';
                }
                e.target.disabled = true;
                e.target.textContent = 'Confirmed ✓';
            }
        });
    });
}

function initCitationCopying() {
    const copyBtns = document.querySelectorAll('.btn-copy-citation');
    copyBtns.forEach(btn => {
        btn.addEventListener('click', (e) => {
            const text = e.target.dataset.citationText;
            if (text) {
                navigator.clipboard.writeText(text).then(() => {
                    const originalText = e.target.textContent;
                    e.target.textContent = 'Copied!';
                    setTimeout(() => e.target.textContent = originalText, 2000);
                });
            }
        });
    });
}

// Universal Copilot Speech-to-Text Controller for All Personas
var activeRecognition = null;
var activeMicBtn = null;
var activeInput = null;
var activeStatus = null;
var transcriptBase = '';

function toggleCopilotSpeech(btnId, inputId, statusId) {
    var btn = typeof btnId === 'string' ? document.getElementById(btnId) : btnId;
    var input = typeof inputId === 'string' ? document.getElementById(inputId) : inputId;
    var status = statusId ? (typeof statusId === 'string' ? document.getElementById(statusId) : statusId) : null;

    if (!btn || !input) return;

    // If currently listening on this exact button, toggle OFF
    if (activeRecognition && activeMicBtn === btn) {
        stopCopilotSpeech();
        return;
    }

    // If listening on another button, stop that first
    if (activeRecognition) {
        stopCopilotSpeech();
    }

    startCopilotSpeech(btn, input, status);
}

function toggleApplicantSpeech() {
    toggleCopilotSpeech('btnApplicantMic', 'streamQuestionInput', 'applicantSpeechStatus');
}

function toggleOfficerSpeech() {
    toggleCopilotSpeech('btnOfficerMic', 'officerQInput', 'officerSpeechStatus');
}

function toggleComplianceSpeech() {
    toggleCopilotSpeech('btnCompMic', 'compQInput', 'compSpeechStatus');
}

function startCopilotSpeech(btn, input, status) {
    var SpeechRecognition = window.SpeechRecognition || window.webkitSpeechRecognition;

    if (!SpeechRecognition) {
        showCopilotSpeechStatus(status, "Voice recognition is not supported in this browser. Please use Google Chrome, Microsoft Edge, or Safari, or type your question.", true);
        return;
    }

    try {
        var recognition = new SpeechRecognition();
        recognition.continuous = true;
        recognition.interimResults = true;
        recognition.lang = 'en-US';

        activeRecognition = recognition;
        activeMicBtn = btn;
        activeInput = input;
        activeStatus = status;

        // Capture existing text before voice input so we append seamlessly
        var existingText = (input.value || '').trim();
        transcriptBase = existingText ? existingText + ' ' : '';

        recognition.onstart = function () {
            setMicUIState(btn, true);
            showCopilotSpeechStatus(status, "Listening... Speak your question. Tap the microphone again when finished.", false);
        };

        recognition.onresult = function (event) {
            var interimText = '';
            var finalSessionText = '';

            for (var i = 0; i < event.results.length; ++i) {
                var res = event.results[i];
                if (res.isFinal) {
                    finalSessionText += res[0].transcript.trim() + ' ';
                } else {
                    interimText += res[0].transcript;
                }
            }

            input.value = (transcriptBase + finalSessionText + interimText).trim();
        };

        recognition.onerror = function (event) {
            console.warn('Speech recognition error:', event.error);
            var err = event.error;

            if (err === 'aborted') {
                stopCopilotSpeech();
                return;
            }

            stopCopilotSpeech();

            if (err === 'not-allowed' || err === 'permission-denied') {
                showCopilotSpeechStatus(status, "Microphone access was denied. Please allow microphone permissions in your browser address bar settings.", true);
            } else if (err === 'no-speech') {
                showCopilotSpeechStatus(status, "No speech detected. Tap the microphone to speak again.", false);
            } else if (err === 'audio-capture') {
                showCopilotSpeechStatus(status, "No microphone detected. Ensure your microphone is connected and enabled.", true);
            } else if (err === 'network') {
                showCopilotSpeechStatus(status, "Speech service network error. Please verify your internet connection.", true);
            } else {
                showCopilotSpeechStatus(status, "Voice input note: " + err, true);
            }
        };

        recognition.onend = function () {
            if (activeRecognition === recognition) {
                stopCopilotSpeech();
            }
        };

        recognition.start();
    } catch (err) {
        console.error('Speech initialization error:', err);
        stopCopilotSpeech();
        showCopilotSpeechStatus(status, "Voice recognition could not start: " + (err.message || err), true);
    }
}

function stopCopilotSpeech() {
    if (activeRecognition) {
        try {
            activeRecognition.abort();
        } catch (e) { }
        activeRecognition = null;
    }

    if (activeMicBtn) {
        setMicUIState(activeMicBtn, false);
        activeMicBtn = null;
    }

    if (activeStatus && !activeStatus.classList.contains('error')) {
        var st = activeStatus;
        setTimeout(function () {
            if (!activeRecognition && st) {
                st.style.display = 'none';
            }
        }, 2000);
    }

    activeInput = null;
    activeStatus = null;
}

function setMicUIState(btn, listening) {
    if (!btn) return;
    if (listening) {
        btn.classList.add('listening');
        btn.setAttribute('aria-pressed', 'true');
        btn.setAttribute('aria-label', 'Stop voice input');
        btn.setAttribute('title', 'Stop voice input');
    } else {
        btn.classList.remove('listening');
        btn.setAttribute('aria-pressed', 'false');
        btn.setAttribute('aria-label', 'Start voice input');
        btn.setAttribute('title', 'Start voice input');
    }
}

function showCopilotSpeechStatus(statusElem, msg, isError) {
    if (!statusElem) return;
    statusElem.textContent = msg;
    statusElem.className = 'copilot-speech-status' + (isError ? ' error' : '');
    statusElem.style.display = 'flex';

    if (isError) {
        setTimeout(function () {
            if (statusElem) statusElem.style.display = 'none';
        }, 6000);
    }
}

// Explicit window assignments for compatibility
window.toggleCopilotSpeech = toggleCopilotSpeech;
window.toggleApplicantSpeech = toggleApplicantSpeech;
window.toggleOfficerSpeech = toggleOfficerSpeech;
window.toggleComplianceSpeech = toggleComplianceSpeech;
window.stopCopilotSpeech = stopCopilotSpeech;

