(function () {
  // Init Bootstrap toasts if present
  document.querySelectorAll('.toast').forEach(function (el) {
    try {
      var t = new bootstrap.Toast(el, { delay: 3500, autohide: true });
      t.show();
    } catch { /* ignore */ }
  });

  // Simple global AJAX spinner (optional)
  const spinnerId = 'globalSpinner';
  const spinner = document.createElement('div');
  spinner.id = spinnerId;
  spinner.style.display = 'none';
  spinner.style.position = 'fixed';
  spinner.style.top = '0';
  spinner.style.left = '0';
  spinner.style.width = '100%';
  spinner.style.height = '100%';
  spinner.style.background = 'rgba(255,255,255,.4)';
  spinner.style.zIndex = '2000';
  spinner.innerHTML = '<div style="position:absolute;top:50%;left:50%;transform:translate(-50%,-50%);" class="spinner-border text-primary" role="status"><span class="visually-hidden">Loading...</span></div>';
  document.body.appendChild(spinner);

  let activeAjax = 0;
  const show = () => spinner.style.display = 'block';
  const hide = () => spinner.style.display = 'none';

  if (window.jQuery) {
    $(document).ajaxStart(function () {
      activeAjax++;
      show();
    });
    $(document).ajaxStop(function () {
      activeAjax = Math.max(0, activeAjax - 1);
      if (activeAjax === 0) hide();
    });
  }
})();