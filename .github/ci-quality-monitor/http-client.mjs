export class HttpClient {
  constructor(fetchImplementation = fetch) {
    this.fetch = fetchImplementation;
  }

  async response(url, accept = "application/json") {
    const response = await this.fetch(url, {
      headers: { Accept: accept, "User-Agent": "dotnet-sdk-ci-quality-monitor" }
    });
    if (!response.ok) {
      throw new Error(`GET ${url} returned ${response.status} ${response.statusText}.`);
    }
    return response;
  }

  async json(url) {
    return (await this.response(url)).json();
  }
}
