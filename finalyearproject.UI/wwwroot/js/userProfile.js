// User profile page scripts: load info, skills, and handle CV edit

const levelMap = { 1: 'Student / Final Year / Intern', 2: '0-3 Yrs', 3: '3-5 Yrs', 4: '5+ Yrs' };
const levelClass = { 1: 'level-1', 2: 'level-2', 3: 'level-3', 4: 'level-4' };

$(function () {
    // Load user info (including current portfolio URL)
    fetch('/User/GetUserInfo')
        .then(r => r.json())
        .then(info => {
            $('#userEmail').text(info.email || '—');
            $('#userPhone').text(info.phoneNumber || '—');
            $('#userJoined').text(
                info.joinedDate
                    ? 'Joined ' + new Date(info.joinedDate).toLocaleDateString('en-US', { month: 'short', year: 'numeric' })
                    : '—'
            );

            if (info.portfolioUrl) {
                $('#editPortfolioUrl').val(info.portfolioUrl);
            }
        });

    // Load skills for card display (one card per skill/sub-skill, with its own rating)
    fetch('/User/GetUserSkillsJson')
        .then(r => r.json())
        .then(skills => {
            const container = document.getElementById('skillsContainer');

            if (!skills || !skills.length) {
                document.getElementById('statSkills').textContent = '0';
                container.innerHTML = `
                    <div class="empty-skills" style="grid-column:1/-1;">
                        <div class="icon">🎯</div>
                        <p>You haven't added any skills yet.</p>
                        <a href="/Account/AddSkills" class="action-btn action-btn-primary" style="display:inline-flex;">
                            ➕ Add Your First Skill
                        </a>
                    </div>`;
                return;
            }

            document.getElementById('statSkills').textContent = skills.length;

            container.innerHTML = skills.map(function (s) {
                const lvl = s.experienceLevel || s.ExperienceLevel || 0;
                const days = s.availableDays || s.AvailableDays || '';
                const start = s.availableTimeStart || s.AvailableTimeStart || '';
                const end = s.availableTimeEnd || s.AvailableTimeEnd || '';
                const timeStr = (start && end) ? (start + ' – ' + end) : '';

                const rating = s.skillRating ?? s.SkillRating;
                const reviewCount = s.skillReviewCount ?? s.SkillReviewCount;
                const hasRating = rating != null && reviewCount != null && reviewCount > 0;
                const ratingHtml = hasRating
                    ? `<span class="skill-avail">⭐ ${Number(rating).toFixed(1)} (${reviewCount})</span>`
                    : '';

                return `
                    <div class="skill-card">
                        <div class="skill-field">${s.fieldName || s.FieldName || '—'}</div>
                        <div class="skill-name">${s.skillName || s.SkillName || '—'}</div>
                        <div class="skill-sub">${s.subSkillName || s.SubSkillName || '—'}</div>
                        <div class="skill-footer">
                            <span class="badge-level ${levelClass[lvl] || 'level-1'}">
                                ${levelMap[lvl] || 'N/A'}
                            </span>
                            ${days ? `<span class="skill-avail">📅 ${days}</span>` : ''}
                            ${timeStr ? `<span class="skill-avail">🕐 ${timeStr}</span>` : ''}
                            ${ratingHtml}
                        </div>
                    </div>`;
            }).join('');
        });

    // Load profile statistics (sessions, rating, points)
    fetch('/User/GetProfileStats')
        .then(r => r.ok ? r.json() : null)
        .then(stats => {
            if (!stats) return;

            const sessionsEl = document.getElementById('statSessions');
            const pointsEl = document.getElementById('statPoints');

            if (sessionsEl) sessionsEl.textContent = stats.sessionsCompleted ?? stats.SessionsCompleted ?? 0;

            if (pointsEl) pointsEl.textContent = stats.pointsEarned ?? stats.PointsEarned ?? 0;
        })
        .catch(() => {
            // Fail silently; profile still works without stats.
        });

    // Open CV edit modal
    $('#btnEditCv').on('click', function () {
        const modalEl = document.getElementById('editCvModal');
        const modal = bootstrap.Modal.getOrCreateInstance(modalEl);
        modal.show();
    });

    // Save CV & portfolio changes
    $('#btnSaveCvEdit').on('click', function () {
        const fileInput = document.getElementById('editCvFile');
        const cvFile = fileInput.files[0] || null;
        const portfolio = $('#editPortfolioUrl').val() || '';

        const formData = new FormData();
        formData.append('cv', cvFile);
        formData.append('portfolioUrl', portfolio);

        $.ajax({
            url: '/Account/UpdateDocumentsForLoggedIn',
            type: 'POST',
            data: formData,
            processData: false,
            contentType: false
        }).done(function (data) {
            if (data && data.success) {
                var modalEl = document.getElementById('editCvModal');
                var modal = bootstrap.Modal.getInstance(modalEl) || bootstrap.Modal.getOrCreateInstance(modalEl);
                modal.hide();
                $('#editCvFile').val('');
                var msg = cvFile
                    ? 'CV and portfolio saved successfully. Your changes have been uploaded.'
                    : 'Portfolio URL saved successfully.';
                DevExpress.ui.notify(msg, 'success', 3000);
            } else {
                DevExpress.ui.notify((data && data.message) || 'Error updating documents', 'error', 2500);
            }
        }).fail(function () {
            DevExpress.ui.notify('Error updating documents', 'error', 2500);
        });
    });
});