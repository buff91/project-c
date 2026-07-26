-- Build one editable Project-C animation source from a JSON manifest.
--
-- The Python runner owns shot selection and deterministic draft frame
-- generation. This script owns the Aseprite contracts: canvas, palette,
-- frame duration, tags, repeat mode, and editable .aseprite output.

local function required(name)
  local value = app.params[name]
  if value == nil or value == "" then
    error("missing --script-param " .. name .. "=...")
  end
  return value
end

local function readAll(path)
  local file, openError = io.open(path, "rb")
  if file == nil then
    error("cannot open manifest " .. path .. ": " .. tostring(openError))
  end
  local content = file:read("*a")
  file:close()
  return content
end

local manifestPath = required("manifest")
local manifest = json.decode(readAll(manifestPath))
-- Aseprite's native JSON value is table-like userdata in some releases, so
-- validate by shape instead of Lua's type() result.
if manifest == nil then
  error("animation manifest must decode to an object")
end
if manifest.canvas == nil or tonumber(manifest.canvas[1]) == nil or
   tonumber(manifest.canvas[2]) == nil then
  error("animation manifest canvas must be [width, height]")
end
if manifest.clips == nil or #manifest.clips == 0 then
  error("animation manifest must contain clips")
end

local width = tonumber(manifest.canvas[1])
local height = tonumber(manifest.canvas[2])
local output = manifest.output
local palettePath = manifest.palette
if not app.fs.isFile(palettePath) then
  error("palette does not exist: " .. tostring(palettePath))
end

local sprite = Sprite(width, height, ColorMode.RGB)
app.sprite = sprite
local layer = sprite.layers[1]
layer.name = "base"
if #sprite.cels > 0 then
  sprite:deleteCel(sprite.cels[1])
end

local palette = Palette{ fromFile=palettePath }
if palette == nil or #palette == 0 then
  sprite:close()
  error("could not load palette: " .. palettePath)
end
sprite:setPalette(palette)

local frameNumber = 0
for clipIndex = 1, #manifest.clips do
  local clip = manifest.clips[clipIndex]
  if type(clip.tag) ~= "string" or clip.tag == "" then
    sprite:close()
    error("clip tag is required")
  end
  if clip.frames == nil or #clip.frames == 0 then
    sprite:close()
    error("clip " .. clip.tag .. " has no frames")
  end

  local fromFrame = frameNumber + 1
  for frameIndex = 1, #clip.frames do
    local frameSpec = clip.frames[frameIndex]
    local source = frameSpec.source
    if not app.fs.isFile(source) then
      sprite:close()
      error("frame source does not exist: " .. tostring(source))
    end
    local image = Image{ fromFile=source }
    if image == nil then
      sprite:close()
      error("could not load frame source: " .. source)
    end
    if image.width ~= width or image.height ~= height then
      sprite:close()
      error(
        "frame source canvas mismatch: " .. source ..
        " expected " .. tostring(width) .. "x" .. tostring(height) ..
        " got " .. tostring(image.width) .. "x" .. tostring(image.height)
      )
    end

    frameNumber = frameNumber + 1
    if frameNumber > 1 then
      sprite:newEmptyFrame(frameNumber)
    end
    sprite:newCel(layer, frameNumber, image, Point(0, 0))
    local durationMs = tonumber(frameSpec.duration_ms or clip.duration_ms or 100)
    if durationMs == nil or durationMs <= 0 then
      sprite:close()
      error("frame duration must be positive")
    end
    sprite.frames[frameNumber].duration = durationMs / 1000.0
  end

  local tag = sprite:newTag(fromFrame, frameNumber)
  tag.name = clip.tag
  tag.aniDir = AniDir.FORWARD
  tag.repeats = clip.loop and 0 or 1
  tag.data = json.encode{
    project_c=true,
    loop=clip.loop and true or false,
    source_manifest=manifestPath
  }
end

sprite.data = json.encode{
  project_c_animation=true,
  schema_version=manifest.schema_version,
  source_manifest=manifestPath
}
sprite:saveAs(output)
sprite:close()

print(
  "built animation " .. output ..
  " (" .. tostring(width) .. "x" .. tostring(height) ..
  ", frames=" .. tostring(frameNumber) ..
  ", clips=" .. tostring(#manifest.clips) .. ")"
)
