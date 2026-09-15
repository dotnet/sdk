export function selectEvaluationCandidates(dossier, scenario)
{
  if (!scenario) return dossier;
  const phases = new Set(scenario.expectedPhases ?? []);
  const failureTypes = new Set(scenario.expectedFailureTypes ?? []);
  const evidenceSources = new Set(scenario.expectedEvidenceSources ?? []);
  const mechanismTerms = (scenario.expectedMechanismIncludes ?? []).map(term => term.toLowerCase());
  const sharedTerms = (scenario.expectedMatchingMechanismIncludes ?? []).map(term => term.toLowerCase());
  const componentTerm = scenario.expectedComponentIncludes?.toLowerCase();
  for (const failure of dossier.failures)
  {
    failure.issueCandidates = (failure.issueCandidates ?? []).filter(candidate =>
    {
      if (phases.size > 0 && !phases.has(candidate.phase)) return false;
      if (failureTypes.size > 0 && !failureTypes.has(candidate.failureType)) return false;
      if ([...evidenceSources].some(source => !candidate.evidenceSources?.includes(source))) return false;
      if (componentTerm && !candidate.component?.toLowerCase().includes(componentTerm)) return false;
      const mechanism = candidate.mechanism?.toLowerCase() ?? "";
      const terms = mechanismTerms.length > 0 ? mechanismTerms : sharedTerms;
      return terms.every(term => mechanism.includes(term));
    });
  }
  dossier.evaluationScenario = {name: scenario.name, evidence: scenario.evidence};
  return dossier;
}
