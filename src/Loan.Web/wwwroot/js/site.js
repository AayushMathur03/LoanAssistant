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
