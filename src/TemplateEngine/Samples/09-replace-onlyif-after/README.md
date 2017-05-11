The sample in this folder demonstrates:

 - **`onlyIf`** - Shows how you can replace some text on the condition it's preceded by a specified string.

In this sample, we show how you can replace a 

From [`site.css`](./MyProject.StarterWeb/wwwroot/css/site.css)

```
body {
    padding-top: 50px;
    padding-bottom: 20px;
    background-color: black;
}

/* Set widths on the form inputs since otherwise they're 100% wide */
input,
select,
textarea {
    max-width: 280px;
    color: black;
}

/* Carousel */
.carousel-caption p {
    font-size: 20px;
    line-height: 1.4;
    color: black
}
```

The sample shows how you can update the `black` for background-color without impacting the values for color in the following elements.


See [`template.json`](./MyProject.StarterWeb/.template.config/template.json)

