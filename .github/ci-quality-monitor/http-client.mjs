export class HttpResponseError extends Error {
  constructor(url, status, statusText) {
    super(`GET ${url} returned ${status} ${statusText}.`);
    this.name = "HttpResponseError";
    this.status = status;
  }
}

export class HttpClient {
  constructor(fetchImplementation = fetch) {
    this.fetch = fetchImplementation;
  }

  async response(url, accept = "application/json") {
    const response = await this.fetch(url, {
      headers: { Accept: accept, "User-Agent": "dotnet-sdk-ci-quality-monitor" }
    });
    if (!response.ok) {
      throw new HttpResponseError(url, response.status, response.statusText);
    }
    return response;
  }

  async json(url) {
    return (await this.response(url)).json();
  }
}
