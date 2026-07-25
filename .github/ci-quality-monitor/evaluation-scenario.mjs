export function selectEvaluationCandidates(dossier, scenario) {
  if (!scenario) return dossier;
  const categories = new Set(scenario.expectedCategories ?? []);
  const mechanismTerms = (scenario.expectedMechanismIncludes ?? []).map(term => term.toLowerCase());
  const sharedTerms = (scenario.expectedMatchingMechanismIncludes ?? []).map(term => term.toLowerCase());
  const componentTerm = scenario.expectedComponentIncludes?.toLowerCase();
  for (const failure of dossier.failures) {
    failure.issueCandidates = (failure.issueCandidates ?? []).filter(candidate => {
      if (categories.size > 0 && !categories.has(candidate.category)) return false;
      if (componentTerm && !candidate.component?.toLowerCase().includes(componentTerm)) return false;
      const mechanism = candidate.mechanism?.toLowerCase() ?? "";
      const terms = mechanismTerms.length > 0 ? mechanismTerms : sharedTerms;
      return terms.every(term => mechanism.includes(term));
    });
  }
  dossier.evaluationScenario = { name: scenario.name, evidence: scenario.evidence };
  return dossier;
}