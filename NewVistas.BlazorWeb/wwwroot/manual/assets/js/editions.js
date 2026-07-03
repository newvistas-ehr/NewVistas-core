/* Site-aware editions for the NewVistas manual.
   When served by the app, fetches the site's enabled feature flags and (a) shows a
   "this site" banner and (b) dims/labels any flag-gated content that's off here.
   When opened as a static file (no server), the fetch fails and the manual is left
   showing everything — a graceful, progressive enhancement. */
(function () {
  "use strict";

  // Heritage classification of each site feature flag — mirrors the tags documented on
  // SiteParametersState ("Site Flavor"): VistA core / RPMS module / Modern enhancement.
  var TIER = {
    PATIENT_MERGE: "vista", EPCS: "vista",
    IMMUNIZATION_FORECAST: "rpms", EXTERNAL_REFERRAL: "rpms", SUBSTANCE_ABUSE_TREATMENT: "rpms",
    PHARMACY_POS: "rpms", GPRA_REPORTING: "rpms", PCC_SURVEILLANCE: "rpms",
    ICARE_DASHBOARD: "rpms", APPOINTMENT_WAITLIST: "rpms",
    PROVIDER_AVAILABILITY: "modern", PROVIDER_UNAVAILABILITY_BATCH: "modern",
    PATIENT_SELF_SCHEDULING: "modern", EXTERNAL_PHARMACY: "modern", ONCOLOGY: "modern", PRECISION_ONCOLOGY: "modern",
    HOME_BASED_CARE: "modern", HOME_HEALTH_MEDICARE: "modern", NEONATAL_CARE: "modern",
    PHARMACOGENOMICS: "modern", HEREDITARY_GENETICS: "modern", SPECIALTY_COVERSHEET: "modern",
    PERSON_IDENTITY: "modern"
  };

  fetch("/api/site/features", { headers: { Accept: "application/json" } })
    .then(function (r) { return r.ok ? r.json() : null; })
    .then(function (data) {
      if (!data || !Array.isArray(data.enabled)) return; // static/offline → leave as-is
      var on = {};
      data.enabled.forEach(function (f) { on[f] = true; });
      apply(on);
    })
    .catch(function () { /* opened as a static file — no server, do nothing */ });

  function apply(on) {
    document.body.classList.add("edition-aware");
    banner(on);

    // Dim + label any flag-gated content whose flag is OFF on this site.
    var nodes = document.querySelectorAll("[data-flag]");
    for (var i = 0; i < nodes.length; i++) {
      var el = nodes[i];
      var flag = el.getAttribute("data-flag");
      if (on[flag]) {
        el.classList.add("edition-on");
      } else {
        el.classList.add("edition-off");
        var note = document.createElement("span");
        note.className = "edition-off-note";
        note.textContent = "Not enabled on this site";
        (el.querySelector(".ct") || el).appendChild(note);
      }
    }
  }

  function banner(on) {
    var hasRpms = false, hasModern = false, mods = [];
    Object.keys(on).forEach(function (f) {
      if (TIER[f] === "rpms") { hasRpms = true; mods.push(f); }
      else if (TIER[f] === "modern") { hasModern = true; mods.push(f); }
    });

    var tiers = '<span class="tier vista">VistA</span>';
    if (hasRpms) tiers += ' <span class="tier rpms">RPMS</span>';
    if (hasModern) tiers += ' <span class="tier modern">Modern</span>';

    var detail = mods.length
      ? ' &nbsp;·&nbsp; modules on: ' + mods.map(function (f) { return '<span class="flag">' + f + "</span>"; }).join(" ")
      : ' &nbsp;·&nbsp; core VistA only';

    var el = document.createElement("div");
    el.className = "site-edition-banner";
    el.innerHTML = "<strong>This site:</strong> " + tiers + detail;

    var topbar = document.querySelector(".topbar");
    if (topbar && topbar.parentNode) {
      topbar.parentNode.insertBefore(el, topbar.nextSibling);
    }
  }
})();
