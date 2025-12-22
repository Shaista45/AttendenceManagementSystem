// Attendance Management System JavaScript

// Auto-dismiss alerts
setTimeout(function () {
    $('.alert').alert('close');
}, 5000);

// Sidebar toggle for mobile
$('.navbar-toggler').on('click', function () {
    $('.sidebar').toggleClass('show');
});

// Search functionality for tables
function initializeTableSearch(inputId, tableId) {
    $(document).ready(function () {
        $('#' + inputId).on('keyup', function () {
            var value = $(this).val().toLowerCase();
            $('#' + tableId + ' tbody tr').filter(function () {
                $(this).toggle($(this).text().toLowerCase().indexOf(value) > -1)
            });
        });
    });
}

// Initialize all table searches
initializeTableSearch('searchInput', 'dataTable');

// Attendance status color coding
function updateAttendanceBadges() {
    $('.badge').each(function () {
        var text = $(this).text().toLowerCase();
        if (text === 'present') {
            $(this).addClass('bg-success');
        } else if (text === 'absent') {
            $(this).addClass('bg-danger');
        } else if (text === 'late') {
            $(this).addClass('bg-warning text-dark');
        }
    });
}

// Update badges on page load
$(document).ready(function () {
    updateAttendanceBadges();
});

// Auto-refresh dashboard data
function refreshDashboardData() {
    // This would typically make an AJAX call to refresh data
    console.log('Refreshing dashboard data...');
}

// Auto-refresh every 30 seconds (optional)
// setInterval(refreshDashboardData, 30000);

// Form validation
function validateAttendanceForm() {
    var isValid = true;
    $('.attendance-status').each(function () {
        if (!$(this).val()) {
            isValid = false;
            $(this).addClass('is-invalid');
        } else {
            $(this).removeClass('is-invalid');
        }
    });
    return isValid;
}

// Export functionality
function exportToExcel(tableId, filename) {
    // This would typically use a library like SheetJS
    console.log('Exporting table ' + tableId + ' to ' + filename);
    alert('Export functionality would be implemented here.');
}

// Print functionality
function printAttendanceReport() {
    window.print();
}

// Date range validation
function validateDateRange(fromDateId, toDateId) {
    var fromDate = new Date($('#' + fromDateId).val());
    var toDate = new Date($('#' + toDateId).val());

    if (fromDate > toDate) {
        alert('From date cannot be after To date');
        return false;
    }
    return true;
}

// Initialize tooltips
$(document).ready(function () {
    $('[data-bs-toggle="tooltip"]').tooltip();
});

// Initialize popovers
$(document).ready(function () {
    $('[data-bs-toggle="popover"]').popover();
});

// Auto-mark attendance for current class
function autoMarkAttendance() {
    if (confirm('Mark yourself as present for the current class?')) {
        // This would call the backend API
        window.location.href = '/Student/AutoMark';
    }
}

// Real-time clock
function updateClock() {
    var now = new Date();
    var time = now.toLocaleTimeString();
    var date = now.toLocaleDateString();
    $('#current-time').text(time + ' | ' + date);
}

// Update clock every second
setInterval(updateClock, 1000);

// Initialize clock on page load
$(document).ready(function () {
    updateClock();
});